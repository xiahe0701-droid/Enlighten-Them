using System;
using UnityEngine;

public enum PuzzleDifficulty
{
    Easy,
    Normal,
    Hard
}

public interface IRepairPuzzle
{
    bool IsOpen { get; }

    void Open(
        PuzzleDifficulty difficulty,
        Action onCompleted
    );

    void Close();
}

public abstract class RepairPuzzleBase :
    MonoBehaviour,
    IRepairPuzzle
{
    public abstract bool IsOpen { get; }

    public abstract void Open(
        PuzzleDifficulty difficulty,
        Action onCompleted
    );

    public abstract void Close();
}