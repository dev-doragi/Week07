using System.Collections.Generic;
using UnityEngine;

public static class IncomeShapeLibrary
{
    private static readonly Vector2Int[] ICells =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(2, 0),
        new Vector2Int(3, 0)
    };

    private static readonly Vector2Int[] JCells =
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(2, 0)
    };

    private static readonly Vector2Int[] LCells =
    {
        new Vector2Int(2, 1),
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(2, 0)
    };

    private static readonly Vector2Int[] OCells =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1)
    };

    private static readonly Vector2Int[] SCells =
    {
        new Vector2Int(1, 1),
        new Vector2Int(2, 1),
        new Vector2Int(0, 0),
        new Vector2Int(1, 0)
    };

    private static readonly Vector2Int[] TCells =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(2, 1),
        new Vector2Int(1, 0)
    };

    private static readonly Vector2Int[] ZCells =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(2, 0)
    };

    private static readonly Vector2Int[] CrossCells =
    {
        new Vector2Int(1, 2),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(2, 1),
        new Vector2Int(1, 0)
    };

    public static IReadOnlyList<Vector2Int> GetBaseCells(IncomeBlockType blockType)
    {
        return blockType switch
        {
            IncomeBlockType.I => ICells,
            IncomeBlockType.J => JCells,
            IncomeBlockType.L => LCells,
            IncomeBlockType.O => OCells,
            IncomeBlockType.S => SCells,
            IncomeBlockType.T => TCells,
            IncomeBlockType.Z => ZCells,
            IncomeBlockType.Cross => CrossCells,
            _ => OCells
        };
    }
}
