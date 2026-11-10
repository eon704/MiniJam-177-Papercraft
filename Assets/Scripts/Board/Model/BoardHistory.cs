using System.Collections.Generic;
using Paperbound.Core;

/// <summary>
/// История Undo — стек состояний ядра. В состоянии есть всё: позиция, форма, бюджет, звёзды,
/// обрушения, террейн, расписания и движущиеся сущности, поэтому новые механики попадают в Undo сами.
/// Нижний элемент — начало уровня, он не снимается.
/// </summary>
public class BoardHistory
{
    private readonly Stack<GameState> _history = new();

    public int Count => _history.Count;

    public GameState Current => _history.Count > 0 ? _history.Peek() : null;

    public void Push(GameState state) => _history.Push(state);

    public GameState Undo()
    {
        if (_history.Count <= 1)
            return null;

        _history.Pop();
        return _history.Peek();
    }

    public void Clear(GameState initial)
    {
        _history.Clear();
        _history.Push(initial);
    }

    public void Reset()
    {
        while (_history.Count > 1)
            _history.Pop();
    }
}
