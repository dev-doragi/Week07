#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(EnemyGridAuthoring))]
public class EnemyGridAuthoringEditor : Editor
{
    private static readonly Vector2Int[] FourDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private EnemyGridAuthoring _authoring;

    private void OnEnable()
    {
        _authoring = (EnemyGridAuthoring)target;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        if (_authoring != null && _authoring.UnitRoot == null)
        {
            _authoring.EnsureUnitRoot();
            EditorUtility.SetDirty(_authoring);
        }

        GUILayout.Space(8f);

        if (GUILayout.Button("Clear Enemy Grid"))
        {
            ClearEnemyGrid();
        }

        if (GUILayout.Button("Validate Grid"))
        {
            ValidateGrid();
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_authoring == null || _authoring != target)
        {
            return;
        }

        if (Selection.activeGameObject != _authoring.gameObject)
        {
            return;
        }

        EnemyGridManager grid = _authoring.Grid;
        if (grid == null)
        {
            return;
        }

        Event evt = Event.current;
        if (evt == null)
        {
            return;
        }

        if (!TryGetMouseWorldOnGridPlane(evt.mousePosition, _authoring.transform.position.z, out Vector3 worldPos))
        {
            return;
        }

        Vector2Int cell = grid.WorldToCell(worldPos);
        bool hasSelectedUnit = _authoring.SelectedUnit != null;
        Vector2Int footprint = hasSelectedUnit ? _authoring.SelectedUnit.Size : Vector2Int.one;
        if (footprint.x < 1) footprint.x = 1;
        if (footprint.y < 1) footprint.y = 1;

        bool canPlace = hasSelectedUnit && CanPlaceForAuthoring(grid, _authoring.SelectedUnit, cell);
        DrawHover(cell, footprint, canPlace, hasSelectedUnit, grid);

        DrawSceneLabel(cell, worldPos, hasSelectedUnit);

        if (evt.alt)
        {
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            if (evt.shift)
            {
                bool removed = grid.TryRemove(cell);
                if (removed)
                {
                    EditorUtility.SetDirty(grid);
                    EditorUtility.SetDirty(_authoring);
                    MarkActiveSceneDirty();
                }
                evt.Use();
                return;
            }

            if (hasSelectedUnit && canPlace)
            {
                grid.PlaceInitial(_authoring.SelectedUnit, cell);
                EditorUtility.SetDirty(grid);
                EditorUtility.SetDirty(_authoring);
                MarkActiveSceneDirty();
                evt.Use();
            }
        }
    }

    private void DrawHover(Vector2Int originCell, Vector2Int size, bool canPlace, bool hasSelectedUnit, EnemyGridManager grid)
    {
        Vector3 centerCell = grid.CellToWorld(originCell);
        float cellSize = grid.CellSize;

        Vector3 center = centerCell + new Vector3(
            (size.x - 1) * cellSize * 0.5f,
            (size.y - 1) * cellSize * 0.5f,
            0f);

        Vector3 cubeSize = new Vector3(size.x * cellSize, size.y * cellSize, 0.01f);

        if (!hasSelectedUnit)
        {
            Handles.color = Color.yellow;
        }
        else
        {
            Handles.color = canPlace ? Color.green : Color.red;
        }

        Handles.DrawWireCube(center, cubeSize);
    }

    private void DrawSceneLabel(Vector2Int cell, Vector3 worldPos, bool hasSelectedUnit)
    {
        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        style.normal.textColor = Color.white;

        string text = hasSelectedUnit
            ? $"Cell: ({cell.x}, {cell.y})"
            : $"Cell: ({cell.x}, {cell.y}) | SelectedUnit is null";

        Handles.Label(worldPos + new Vector3(0.15f, 0.15f, 0f), text, style);
    }

    private bool TryGetMouseWorldOnGridPlane(Vector2 mousePos, float planeZ, out Vector3 worldPos)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

        if (plane.Raycast(ray, out float distance))
        {
            worldPos = ray.GetPoint(distance);
            return true;
        }

        worldPos = default;
        return false;
    }

    private static bool CanPlaceForAuthoring(EnemyGridManager grid, UnitDataSO data, Vector2Int origin)
    {
        if (grid == null || data == null)
        {
            return false;
        }

        if (data.PlacementRule == PlacementRule.InitialOnly)
        {
            // Authoring mode exception:
            // allow placing InitialOnly units as long as footprint is in bounds and empty.
            for (int x = 0; x < data.Size.x; x++)
            {
                for (int y = 0; y < data.Size.y; y++)
                {
                    Vector2Int cell = new Vector2Int(origin.x + x, origin.y + y);
                    if (!grid.IsInBounds(cell) || !grid.IsEmpty(cell))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        return grid.CanPlace(data, origin);
    }

    private void ClearEnemyGrid()
    {
        if (_authoring == null)
        {
            return;
        }

        _authoring.EnsureUnitRoot();

        if (_authoring.UnitRoot == null)
        {
            return;
        }

        for (int i = _authoring.UnitRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = _authoring.UnitRoot.GetChild(i);
            DestroyImmediate(child.gameObject);
        }

        EnemyGridManager grid = _authoring.Grid;
        if (grid != null)
        {
            for (int i = grid.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = grid.transform.GetChild(i);
                if (child.GetComponent<Unit>() != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            EditorUtility.SetDirty(grid);
        }

        EditorUtility.SetDirty(_authoring.UnitRoot);
        EditorUtility.SetDirty(_authoring);
        MarkActiveSceneDirty();
    }

    private void ValidateGrid()
    {
        if (_authoring == null || _authoring.Grid == null)
        {
            Debug.LogError("[EnemyGridAuthoring] Validate failed: EnemyGridAuthoring or EnemyGridManager is missing.");
            return;
        }

        EnemyGridManager grid = _authoring.Grid;
        List<EnemyPlacedUnit> units = grid.GetAllPlacedUnitsSnapshot();
        var occupancy = new Dictionary<Vector2Int, EnemyPlacedUnit>();
        var coreUnits = new List<EnemyPlacedUnit>();
        int errors = 0;
        int warnings = 0;

        if (units.Count == 0)
        {
            Debug.LogWarning("[EnemyGridAuthoring] Validate: grid is empty.", _authoring);
            warnings++;
        }

        for (int i = 0; i < units.Count; i++)
        {
            EnemyPlacedUnit unit = units[i];
            if (unit == null || unit.Data == null)
            {
                Debug.LogError("[EnemyGridAuthoring] Validate: null unit/data found in grid.");
                errors++;
                continue;
            }

            if (unit.Data.Team != TeamType.Enemy)
            {
                Debug.LogWarning($"[EnemyGridAuthoring] Validate: non-enemy unit found ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                warnings++;
            }

            if (unit.Data.Prefab == null)
            {
                Debug.LogError($"[EnemyGridAuthoring] Validate: missing prefab in data ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                errors++;
            }

            if (unit.Data.Category == UnitCategory.Core && unit.Data.Team == TeamType.Enemy)
            {
                coreUnits.Add(unit);
            }

            for (int x = 0; x < unit.Data.Size.x; x++)
            {
                for (int y = 0; y < unit.Data.Size.y; y++)
                {
                    Vector2Int cell = new Vector2Int(unit.OriginCell.x + x, unit.OriginCell.y + y);
                    if (!grid.IsInBounds(cell))
                    {
                        Debug.LogError($"[EnemyGridAuthoring] Validate: out-of-bounds footprint at {cell} ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                        errors++;
                        continue;
                    }

                    if (occupancy.TryGetValue(cell, out EnemyPlacedUnit other) && other != unit)
                    {
                        Debug.LogError($"[EnemyGridAuthoring] Validate: overlap at {cell} ({unit.Data.UnitName} vs {other.Data.UnitName}).", GetContext(unit, _authoring));
                        errors++;
                    }
                    else
                    {
                        occupancy[cell] = unit;
                    }

                    EnemyPlacedUnit registered = grid.GetUnitAt(cell);
                    if (registered != unit)
                    {
                        Debug.LogError($"[EnemyGridAuthoring] Validate: cell registry mismatch at {cell} ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                        errors++;
                    }
                }
            }
        }

        if (coreUnits.Count == 0)
        {
            Debug.LogError("[EnemyGridAuthoring] Validate: missing enemy core.", _authoring);
            errors++;
        }
        else if (coreUnits.Count > 1)
        {
            Debug.LogError($"[EnemyGridAuthoring] Validate: expected one enemy core but found {coreUnits.Count}.", _authoring);
            errors++;
        }

        for (int i = 0; i < units.Count; i++)
        {
            EnemyPlacedUnit unit = units[i];
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.PlacementRule == PlacementRule.InitialOnly) continue;

            if (unit.Data.PlacementRule == PlacementRule.NeedsFoundationBelow)
            {
                for (int x = 0; x < unit.Data.Size.x; x++)
                {
                    Vector2Int below = new Vector2Int(unit.OriginCell.x + x, unit.OriginCell.y - 1);
                    EnemyPlacedUnit belowUnit = grid.GetUnitAt(below);
                    if (belowUnit == null || belowUnit.Data == null || !belowUnit.Data.ActsAsFoundation)
                    {
                        Debug.LogError($"[EnemyGridAuthoring] Validate: foundation rule violation at {below} ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                        errors++;
                        break;
                    }
                }
            }
            else if (unit.Data.PlacementRule == PlacementRule.NeedsAdjacent)
            {
                if (!HasAnyAdjacentUnit(grid, unit))
                {
                    Debug.LogError($"[EnemyGridAuthoring] Validate: adjacency rule violation ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                    errors++;
                }
            }
        }

        if (coreUnits.Count > 0 && units.Count > 0)
        {
            var connected = new HashSet<EnemyPlacedUnit>();
            var queue = new Queue<EnemyPlacedUnit>();

            for (int i = 0; i < coreUnits.Count; i++)
            {
                EnemyPlacedUnit core = coreUnits[i];
                connected.Add(core);
                queue.Enqueue(core);
            }

            while (queue.Count > 0)
            {
                EnemyPlacedUnit current = queue.Dequeue();
                AddAdjacentUnitsToQueue(grid, current, connected, queue);
            }

            for (int i = 0; i < units.Count; i++)
            {
                EnemyPlacedUnit unit = units[i];
                if (unit != null && !connected.Contains(unit))
                {
                    Debug.LogError($"[EnemyGridAuthoring] Validate: disconnected from enemy core ({unit.Data.UnitName}).", GetContext(unit, _authoring));
                    errors++;
                }
            }
        }

        if (errors == 0 && warnings == 0)
        {
            Debug.Log($"[EnemyGridAuthoring] Validate success: {units.Count} units, no issues.", _authoring);
        }
        else if (errors == 0)
        {
            Debug.LogWarning($"[EnemyGridAuthoring] Validate completed: 0 errors, {warnings} warnings.", _authoring);
        }
        else
        {
            Debug.LogError($"[EnemyGridAuthoring] Validate failed: {errors} errors, {warnings} warnings.", _authoring);
        }
    }

    private static bool HasAnyAdjacentUnit(EnemyGridManager grid, EnemyPlacedUnit unit)
    {
        for (int x = 0; x < unit.Data.Size.x; x++)
        {
            for (int y = 0; y < unit.Data.Size.y; y++)
            {
                Vector2Int cell = new Vector2Int(unit.OriginCell.x + x, unit.OriginCell.y + y);
                for (int i = 0; i < FourDirections.Length; i++)
                {
                    Vector2Int neighborCell = cell + FourDirections[i];
                    EnemyPlacedUnit neighbor = grid.GetUnitAt(neighborCell);
                    if (neighbor != null && neighbor != unit)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static void AddAdjacentUnitsToQueue(
        EnemyGridManager grid,
        EnemyPlacedUnit unit,
        HashSet<EnemyPlacedUnit> visited,
        Queue<EnemyPlacedUnit> queue)
    {
        for (int x = 0; x < unit.Data.Size.x; x++)
        {
            for (int y = 0; y < unit.Data.Size.y; y++)
            {
                Vector2Int cell = new Vector2Int(unit.OriginCell.x + x, unit.OriginCell.y + y);
                for (int i = 0; i < FourDirections.Length; i++)
                {
                    Vector2Int neighborCell = cell + FourDirections[i];
                    EnemyPlacedUnit neighbor = grid.GetUnitAt(neighborCell);
                    if (neighbor == null || neighbor == unit || visited.Contains(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private static Object GetContext(EnemyPlacedUnit unit, EnemyGridAuthoring fallback)
    {
        if (unit != null && unit.Instance != null)
        {
            return unit.Instance;
        }

        return fallback;
    }

    private static void MarkActiveSceneDirty()
    {
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
#endif
