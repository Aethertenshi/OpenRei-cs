#!/usr/bin/env python3
"""
Build script for OpenAL Soft cross-platform native binaries.

Usage:
    python build.py --help
    python build.py --platform win-x64
    python build.py --platform all           # builds everything (requires all toolchains)

Output goes to: OpenRei/ThirdParty/<rid>/
"""

import argparse
import os
import platform
import shutil
import subprocess
import sys
import urllib.request
import zipfile
import tarfile

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DOWNLOAD_DIR = os.path.join(SCRIPT_DIR, "downloads")
REPO_ROOT = os.path.normpath(os.path.join(SCRIPT_DIR, "..", ".."))
THIRD_PARTY_DIR = os.path.join(REPO_ROOT, "OpenRei", "ThirdParty")

OPENAL_VERSION = "1.25.2"
SOURCE_URL = f"https://github.com/kcat/openal-soft/archive/refs/tags/{OPENAL_VERSION}.tar.gz"
SOURCE_DIR = os.path.join(DOWNLOAD_DIR, f"openal-soft-{OPENAL_VERSION}")

RID_ALIASES = {
    "win-x64":     "win-x64",
    "win-x86":     "win-x86",
    "win-arm64":   "win-arm64",
    "linux-x64":   "linux-x64",
    "linux-arm64": "linux-arm64",
    "osx-x64":     "osx-x64",
    "osx-arm64":   "osx-arm64",
    "android-arm64": "android-arm64",
    "android-x64":   "android-x64",
}

# Mapping: output filename per platform
OUTPUT_FILES = {
    "win-x64":     "soft_oal.dll",
    "win-x86":     "soft_oal.dll",
    "win-arm64":   "soft_oal.dll",
    "linux-x64":   "libopenal.so.1",
    "linux-arm64": "libopenal.so.1",
    "osx-x64":     "libopenal.1.dylib",
    "osx-arm64":   "libopenal.1.dylib",
    "android-arm64": "libopenal.so",
    "android-x64":   "libopenal.so",
}


def log(msg):
    print(f"[build-openal] {msg}")


def download_source():
    if os.path.isdir(SOURCE_DIR):
        log(f"Source already extracted at {SOURCE_DIR}")
        return

    os.makedirs(DOWNLOAD_DIR, exist_ok=True)
    tarball_path = os.path.join(DOWNLOAD_DIR, f"openal-soft-{OPENAL_VERSION}.tar.gz")

    if not os.path.isfile(tarball_path):
        log(f"Downloading OpenAL Soft {OPENAL_VERSION} source...")
        urllib.request.urlretrieve(SOURCE_URL, tarball_path)
        log("Download complete.")
    else:
        log("Tarball already cached.")

    log("Extracting source...")
    with tarfile.open(tarball_path, "r:gz") as tar:
        tar.extractall(path=DOWNLOAD_DIR)
    log(f"Source extracted to {SOURCE_DIR}")


def ensure_output_dir(rid):
    out = os.path.join(THIRD_PARTY_DIR, rid)
    os.makedirs(out, exist_ok=True)
    return out


def find_vcvars():
    """Locate vcvarsall.bat for Visual Studio."""
    import glob
    patterns = [
        r"C:\Program Files\Microsoft Visual Studio\*\*\VC\Auxiliary\Build\vcvarsall.bat",
        r"C:\Program Files (x86)\Microsoft Visual Studio\*\*\VC\Auxiliary\Build\vcvarsall.bat",
        r"C:\Program Files (x86)\Microsoft Visual Studio\*\*\*\*\VC\Auxiliary\Build\vcvarsall.bat",
    ]
    for pattern in patterns:
        matches = sorted(glob.glob(pattern), reverse=True)
        if matches:
            return matches[0]
    return None


def run_cmake(build_dir, cmake_args):
    cmd = ["cmake", "-S", SOURCE_DIR, "-B", build_dir] + cmake_args
    log(f"Running: {' '.join(cmd)}")
    subprocess.check_call(cmd)
    subprocess.check_call(["cmake", "--build", build_dir, "--config", "Release", "--parallel"])
    subprocess.check_call(["cmake", "--install", build_dir, "--config", "Release"])


def build_win(rid, arch):
    """Build for Windows using MSVC."""
    vcvars = find_vcvars()
    if not vcvars:
        log("ERROR: Visual Studio not found. Cannot build Windows binaries.")
        return False

    install_dir = ensure_output_dir(rid)

    cmake_arch_map = {"win-x64": "x64", "win-x86": "Win32", "win-arm64": "ARM64"}
    cmake_arch = cmake_arch_map[rid]

    build_dir = os.path.join(SCRIPT_DIR, "build", rid)
    # cmake_install_dir uses forward slashes for CMake
    cmake_install = install_dir.replace("\\", "/") if platform.system() == "Windows" else install_dir

    cmake_args = [
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_INSTALL_PREFIX={cmake_install}",
        "-DLIBTYPE=SHARED",
        "-DALSOFT_UTILS=OFF",
        "-DALSOFT_EXAMPLES=OFF",
        "-DALSOFT_TESTS=OFF",
        "-DALSOFT_CONFIG=OFF",
        "-DALSOFT_HRTF_DIR=",
        f"-A{cmake_arch}",
    ]

    try:
        if arch == "x64":
            vc_arch = "x64"
        elif arch == "x86":
            vc_arch = "x86"
        elif arch == "arm64":
            vc_arch = "arm64"

        run_cmake(build_dir, cmake_args)

        # cmake installs as OpenAL32.dll on Windows; we need soft_oal.dll
        found = False
        for root, dirs, files in os.walk(install_dir):
            for f in files:
                if f.endswith(".dll"):
                    full = os.path.join(root, f)
                    target = os.path.join(install_dir, "soft_oal.dll")
                    shutil.copy2(full, target)
                    log(f"Copied {full} -> {target}")
                    found = True
                    break
            if found:
                break

        # Clean up extra install artifacts
        for d in ["bin", "lib", "include", "share"]:
            p = os.path.join(install_dir, d)
            if os.path.isdir(p):
                shutil.rmtree(p)

        # Remove any debug suffix
        debug_dll = os.path.join(install_dir, "soft_oald.dll")
        if os.path.isfile(debug_dll):
            os.remove(debug_dll)

        dll_path = os.path.join(install_dir, "soft_oal.dll")
        if os.path.isfile(dll_path):
            size = os.path.getsize(dll_path)
            log(f"OK: {rid} -> soft_oal.dll ({size / 1024:.1f} KB)")
            return True
        else:
            log(f"ERROR: soft_oal.dll not produced for {rid}")
            return False

    except subprocess.CalledProcessError as e:
        log(f"ERROR: Build failed for {rid}: {e}")
        return False


def build_linux(rid, toolchain_file=None):
    """Build for Linux (native or cross-compile)."""
    install_dir = ensure_output_dir(rid)
    build_dir = os.path.join(SCRIPT_DIR, "build", rid)
    cmake_install = install_dir.replace("\\", "/")

    cmake_args = [
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_INSTALL_PREFIX={cmake_install}",
        "-DLIBTYPE=SHARED",
        "-DALSOFT_UTILS=OFF",
        "-DALSOFT_EXAMPLES=OFF",
        "-DALSOFT_TESTS=OFF",
        "-DALSOFT_CONFIG=OFF",
        "-DALSOFT_HRTF_DIR=",
        "-DCMAKE_POSITION_INDEPENDENT_CODE=ON",
    ]

    if toolchain_file:
        cmake_args.append(f"-DCMAKE_TOOLCHAIN_FILE={toolchain_file}")

    try:
        run_cmake(build_dir, cmake_args)

        # Find the .so file
        for root, dirs, files in os.walk(install_dir):
            for f in files:
                if f.startswith("libopenal") and f.endswith(".so"):
                    full = os.path.join(root, f)
                    target = os.path.join(install_dir, "libopenal.so.1")
                    shutil.copy2(full, target)
                    log(f"Copied: {full} -> {target}")
                    return True

        log(f"WARNING: libopenal.so not found in install dir for {rid}")
        return False

    except subprocess.CalledProcessError as e:
        log(f"ERROR: Build failed for {rid}: {e}")
        return False


def build_macos(rid, toolchain_file=None):
    """Build for macOS (requires Xcode or osxcross)."""
    install_dir = ensure_output_dir(rid)
    build_dir = os.path.join(SCRIPT_DIR, "build", rid)
    cmake_install = install_dir.replace("\\", "/")

    cmake_args = [
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_INSTALL_PREFIX={cmake_install}",
        "-DLIBTYPE=SHARED",
        "-DALSOFT_UTILS=OFF",
        "-DALSOFT_EXAMPLES=OFF",
        "-DALSOFT_TESTS=OFF",
        "-DALSOFT_CONFIG=OFF",
        "-DALSOFT_HRTF_DIR=",
    ]

    if toolchain_file:
        cmake_args.append(f"-DCMAKE_TOOLCHAIN_FILE={toolchain_file}")

    try:
        run_cmake(build_dir, cmake_args)

        for root, dirs, files in os.walk(install_dir):
            for f in files:
                if f.startswith("libopenal") and f.endswith(".dylib"):
                    full = os.path.join(root, f)
                    target = os.path.join(install_dir, "libopenal.1.dylib")
                    shutil.copy2(full, target)
                    log(f"Copied: {full} -> {target}")
                    return True

        log(f"WARNING: libopenal.dylib not found in install dir for {rid}")
        return False

    except subprocess.CalledProcessError as e:
        log(f"ERROR: Build failed for {rid}: {e}")
        return False


def build_android(rid, ndk_path=None):
    """Build for Android using the NDK."""
    if not ndk_path:
        ndk_path = os.environ.get("ANDROID_NDK_HOME")

    if not ndk_path:
        log(f"ERROR: Android NDK not found. Set ANDROID_NDK_HOME or pass --ndk-path")
        return False

    toolchain_file = os.path.join(ndk_path, "build", "cmake", "android.toolchain.cmake")
    if not os.path.isfile(toolchain_file):
        log(f"ERROR: Android toolchain not found at {toolchain_file}")
        return False

    arch_map = {
        "android-arm64": "arm64-v8a",
        "android-x64": "x86_64",
    }
    abi = arch_map.get(rid)
    if not abi:
        log(f"ERROR: Unknown Android RID: {rid}")
        return False

    install_dir = ensure_output_dir(rid)
    build_dir = os.path.join(SCRIPT_DIR, "build", rid)
    cmake_install = install_dir.replace("\\", "/")

    cmake_args = [
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_INSTALL_PREFIX={cmake_install}",
        f"-DCMAKE_TOOLCHAIN_FILE={toolchain_file}",
        f"-DANDROID_ABI={abi}",
        "-DANDROID_PLATFORM=android-24",
        "-DLIBTYPE=SHARED",
        "-DALSOFT_UTILS=OFF",
        "-DALSOFT_EXAMPLES=OFF",
        "-DALSOFT_TESTS=OFF",
        "-DALSOFT_CONFIG=OFF",
        "-DALSOFT_HRTF_DIR=",
    ]

    try:
        run_cmake(build_dir, cmake_args)

        for root, dirs, files in os.walk(install_dir):
            for f in files:
                if f.startswith("libopenal") and f.endswith(".so"):
                    full = os.path.join(root, f)
                    target = os.path.join(install_dir, "libopenal.so")
                    shutil.copy2(full, target)
                    log(f"Copied: {full} -> {target}")
                    return True

        log(f"WARNING: libopenal.so not found in install dir for {rid}")
        return False

    except subprocess.CalledProcessError as e:
        log(f"ERROR: Build failed for {rid}: {e}")
        return False


def build_platform(rid, ndk_path=None, toolchain=None, android_ndk=None):
    download_source()

    if rid.startswith("win"):
        # Determine MSVC architecture
        arch_map = {"win-x64": "x64", "win-x86": "x86", "win-arm64": "arm64"}
        return build_win(rid, arch_map[rid])
    elif rid.startswith("linux"):
        if platform.system() == "Linux":
            return build_linux(rid)
        else:
            log(f"Cross-compiling {rid} requires a toolchain file (--toolchain).")
            log(f"Example: python build.py --platform {rid} --toolchain /path/to/linux.cmake")
            return build_linux(rid, toolchain_file=toolchain)
    elif rid.startswith("osx"):
        if platform.system() == "Darwin":
            return build_macos(rid)
        else:
            log(f"Building {rid} requires macOS or an osxcross toolchain.")
            log(f"See: https://github.com/tpoechtrager/osxcross")
            if toolchain:
                return build_macos(rid, toolchain_file=toolchain)
            return False
    elif rid.startswith("android"):
        return build_android(rid, ndk_path=android_ndk or ndk_path)
    else:
        log(f"Unknown platform: {rid}")
        return False


def print_help_extra():
    print(f"""
OpenAL Soft Cross-Platform Build Script v{OPENAL_VERSION}
=========================================================

Build targets:
  python build.py --platform win-x64     (MSVC, available on Windows)
  python build.py --platform linux-x64   (native on Linux, or cross with --toolchain)
  python build.py --platform osx-arm64   (native on macOS, or cross with osxcross)
  python build.py --platform android-arm64  (requires Android NDK)

Prerequisites:
  - CMake >= 3.16
  - C++ compiler matching target platform
  - Android NDK (for Android targets)
  - osxcross (for macOS cross-compilation from Linux)

Output:
  OpenRei/ThirdParty/<rid>/<library>

Environment variables:
  ANDROID_NDK_HOME  - path to Android NDK
""")


def main():
    parser = argparse.ArgumentParser(
        description="Build OpenAL Soft for cross-platform distribution",
        add_help=False
    )
    parser.add_argument("--platform", "-p", default="win-x64",
                        help=f"Target RID: {', '.join(RID_ALIASES.keys())}, or 'all'")
    parser.add_argument("--toolchain", "-t", default=None,
                        help="Path to CMake toolchain file for cross-compilation")
    parser.add_argument("--android-ndk", "-n", default=None,
                        help="Path to Android NDK (or set ANDROID_NDK_HOME)")
    parser.add_argument("--help", "-h", action="store_true")
    args = parser.parse_args()

    if args.help:
        parser.print_help()
        print_help_extra()
        return

    if args.platform == "all":
        platforms = list(RID_ALIASES.keys())
        log(f"Building all {len(platforms)} platforms...")
        for p in platforms:
            log(f"\n{'='*60}")
            log(f"Building {p}...")
            log(f"{'='*60}")
            build_platform(p, toolchain=args.toolchain, android_ndk=args.android_ndk)
    else:
        if args.platform not in RID_ALIASES:
            log(f"Unknown platform: {args.platform}")
            log(f"Available: {', '.join(RID_ALIASES.keys())}")
            sys.exit(1)
        build_platform(args.platform, toolchain=args.toolchain, android_ndk=args.android_ndk)

    log("Done.")


if __name__ == "__main__":
    main()
