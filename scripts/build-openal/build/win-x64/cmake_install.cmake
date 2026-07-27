# Install script for directory: E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2

# Set the install prefix
if(NOT DEFINED CMAKE_INSTALL_PREFIX)
  set(CMAKE_INSTALL_PREFIX "E:/ProjectTemp-Server/OpenRei-cs/OpenRei/ThirdParty/win-x64")
endif()
string(REGEX REPLACE "/$" "" CMAKE_INSTALL_PREFIX "${CMAKE_INSTALL_PREFIX}")

# Set the install configuration name.
if(NOT DEFINED CMAKE_INSTALL_CONFIG_NAME)
  if(BUILD_TYPE)
    string(REGEX REPLACE "^[^A-Za-z0-9_]+" ""
           CMAKE_INSTALL_CONFIG_NAME "${BUILD_TYPE}")
  else()
    set(CMAKE_INSTALL_CONFIG_NAME "Release")
  endif()
  message(STATUS "Install configuration: \"${CMAKE_INSTALL_CONFIG_NAME}\"")
endif()

# Set the component getting installed.
if(NOT CMAKE_INSTALL_COMPONENT)
  if(COMPONENT)
    message(STATUS "Install component: \"${COMPONENT}\"")
    set(CMAKE_INSTALL_COMPONENT "${COMPONENT}")
  else()
    set(CMAKE_INSTALL_COMPONENT)
  endif()
endif()

# Is this installation the result of a crosscompile?
if(NOT DEFINED CMAKE_CROSSCOMPILING)
  set(CMAKE_CROSSCOMPILING "FALSE")
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Dd][Ee][Bb][Uu][Gg])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib" TYPE STATIC_LIBRARY OPTIONAL FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/Debug/OpenAL32.lib")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib" TYPE STATIC_LIBRARY OPTIONAL FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/Release/OpenAL32.lib")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Mm][Ii][Nn][Ss][Ii][Zz][Ee][Rr][Ee][Ll])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib" TYPE STATIC_LIBRARY OPTIONAL FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/MinSizeRel/OpenAL32.lib")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ww][Ii][Tt][Hh][Dd][Ee][Bb][Ii][Nn][Ff][Oo])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib" TYPE STATIC_LIBRARY OPTIONAL FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/RelWithDebInfo/OpenAL32.lib")
  endif()
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Dd][Ee][Bb][Uu][Gg])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE SHARED_LIBRARY FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/Debug/OpenAL32.dll")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE SHARED_LIBRARY FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/Release/OpenAL32.dll")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Mm][Ii][Nn][Ss][Ii][Zz][Ee][Rr][Ee][Ll])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE SHARED_LIBRARY FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/MinSizeRel/OpenAL32.dll")
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ww][Ii][Tt][Hh][Dd][Ee][Bb][Ii][Nn][Ff][Oo])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/bin" TYPE SHARED_LIBRARY FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/RelWithDebInfo/OpenAL32.dll")
  endif()
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Dd][Ee][Bb][Uu][Gg])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/include/AL" TYPE FILE FILES
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/al.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alc.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alext.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx-presets.h"
      )
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/include/AL" TYPE FILE FILES
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/al.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alc.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alext.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx-presets.h"
      )
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Mm][Ii][Nn][Ss][Ii][Zz][Ee][Rr][Ee][Ll])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/include/AL" TYPE FILE FILES
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/al.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alc.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alext.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx-presets.h"
      )
  elseif(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ww][Ii][Tt][Hh][Dd][Ee][Bb][Ii][Nn][Ff][Oo])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/include/AL" TYPE FILE FILES
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/al.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alc.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/alext.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx.h"
      "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/include/AL/efx-presets.h"
      )
  endif()
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  if(EXISTS "$ENV{DESTDIR}${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL/OpenALTargets.cmake")
    file(DIFFERENT _cmake_export_file_changed FILES
         "$ENV{DESTDIR}${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL/OpenALTargets.cmake"
         "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/CMakeFiles/Export/7997f1405615f9851461070a24c70e81/OpenALTargets.cmake")
    if(_cmake_export_file_changed)
      file(GLOB _cmake_old_config_files "$ENV{DESTDIR}${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL/OpenALTargets-*.cmake")
      if(_cmake_old_config_files)
        string(REPLACE ";" ", " _cmake_old_config_files_text "${_cmake_old_config_files}")
        message(STATUS "Old export file \"$ENV{DESTDIR}${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL/OpenALTargets.cmake\" will be replaced.  Removing files [${_cmake_old_config_files_text}].")
        unset(_cmake_old_config_files_text)
        file(REMOVE ${_cmake_old_config_files})
      endif()
      unset(_cmake_old_config_files)
    endif()
    unset(_cmake_export_file_changed)
  endif()
  file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/CMakeFiles/Export/7997f1405615f9851461070a24c70e81/OpenALTargets.cmake")
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Dd][Ee][Bb][Uu][Gg])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/CMakeFiles/Export/7997f1405615f9851461070a24c70e81/OpenALTargets-debug.cmake")
  endif()
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Mm][Ii][Nn][Ss][Ii][Zz][Ee][Rr][Ee][Ll])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/CMakeFiles/Export/7997f1405615f9851461070a24c70e81/OpenALTargets-minsizerel.cmake")
  endif()
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ww][Ii][Tt][Hh][Dd][Ee][Bb][Ii][Nn][Ff][Oo])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/CMakeFiles/Export/7997f1405615f9851461070a24c70e81/OpenALTargets-relwithdebinfo.cmake")
  endif()
  if(CMAKE_INSTALL_CONFIG_NAME MATCHES "^([Rr][Ee][Ll][Ee][Aa][Ss][Ee])$")
    file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/CMakeFiles/Export/7997f1405615f9851461070a24c70e81/OpenALTargets-release.cmake")
  endif()
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/pkgconfig" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/openal.pc")
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/lib/cmake/OpenAL" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/OpenALConfig.cmake")
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/share/openal" TYPE FILE FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/alsoftrc.sample")
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/share/openal" TYPE DIRECTORY FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/hrtf")
endif()

if(CMAKE_INSTALL_COMPONENT STREQUAL "Unspecified" OR NOT CMAKE_INSTALL_COMPONENT)
  file(INSTALL DESTINATION "${CMAKE_INSTALL_PREFIX}/share/openal" TYPE DIRECTORY FILES "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/downloads/openal-soft-1.25.2/presets")
endif()

string(REPLACE ";" "\n" CMAKE_INSTALL_MANIFEST_CONTENT
       "${CMAKE_INSTALL_MANIFEST_FILES}")
if(CMAKE_INSTALL_LOCAL_ONLY)
  file(WRITE "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/install_local_manifest.txt"
     "${CMAKE_INSTALL_MANIFEST_CONTENT}")
endif()
if(CMAKE_INSTALL_COMPONENT)
  if(CMAKE_INSTALL_COMPONENT MATCHES "^[a-zA-Z0-9_.+-]+$")
    set(CMAKE_INSTALL_MANIFEST "install_manifest_${CMAKE_INSTALL_COMPONENT}.txt")
  else()
    string(MD5 CMAKE_INST_COMP_HASH "${CMAKE_INSTALL_COMPONENT}")
    set(CMAKE_INSTALL_MANIFEST "install_manifest_${CMAKE_INST_COMP_HASH}.txt")
    unset(CMAKE_INST_COMP_HASH)
  endif()
else()
  set(CMAKE_INSTALL_MANIFEST "install_manifest.txt")
endif()

if(NOT CMAKE_INSTALL_LOCAL_ONLY)
  file(WRITE "E:/ProjectTemp-Server/OpenRei-cs/scripts/build-openal/build/win-x64/${CMAKE_INSTALL_MANIFEST}"
     "${CMAKE_INSTALL_MANIFEST_CONTENT}")
endif()
