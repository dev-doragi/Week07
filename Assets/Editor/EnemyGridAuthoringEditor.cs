#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(EnemyGridAuthoring))]
public class EnemyGridAuthoringEditor : Editor
{
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
            string name = _authoring != null ? _authoring.name : "EnemyGridAuthoring";
            Debug.Log($"[EnemyGridAuthoring] Validate Grid called on {name}");
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

    private static void MarkActiveSceneDirty()
    {
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
#endif
