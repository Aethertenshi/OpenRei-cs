namespace reistar.Core;

using System.Collections.Generic;

public sealed class PointManager
{
    private readonly List<IPoint> _points = new();
    private EngineContext? _context;

    public void Initialize(EngineContext context)
    {
        _context = context;
        foreach (var p in _points)
        {
            p.OnAttach(_context);
        }
    }

    public T AttachPoint<T>(T point) where T : IPoint
    {
        _points.Add(point);
        if (_context != null)
        {
            point.OnAttach(_context);
        }
        return point;
    }

    public void DetachPoint(IPoint point)
    {
        if (_points.Remove(point))
        {
            point.OnDetach();
        }
    }

    public T? GetPoint<T>() where T : class, IPoint
    {
        foreach (var p in _points)
        {
            if (p is T typed) return typed;
        }
        return null;
    }

    public void UpdatePoints(float deltaTime)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i].Enabled)
            {
                _points[i].OnUpdate(deltaTime);
            }
        }
    }

    public void RenderPoints()
    {
        if (_context == null) return;
        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i].Enabled)
            {
                _points[i].OnRender(_context.Renderer);
            }
        }
    }
}
