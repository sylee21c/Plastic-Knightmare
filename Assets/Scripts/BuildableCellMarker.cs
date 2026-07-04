using UnityEngine;

// 이 컴포넌트가 붙은 GameObject 의 위치에 있는 셀 하나만 브릭 배치 허용.
// 씬에 이런 마커를 여러 개 배치하면 그 셀들만 배치 가능.
// BuildingModeController.restrictToBuildableMarkers = true 여야 활성화됨.
[ExecuteAlways]
public sealed class BuildableCellMarker : MonoBehaviour
{
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 0.4f, 0.55f);
    [SerializeField] private Color selectedGizmoColor = new Color(0.4f, 1f, 0.5f, 0.9f);

    public Vector3 WorldPosition => transform.position;

    public Vector2Int GetCell(BuildingGridOverlay grid)
    {
        if (grid == null || grid.CellSize <= 0f) return Vector2Int.zero;
        return grid.WorldToCell(transform.position);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawGizmo(gizmoColor);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmo(selectedGizmoColor);
    }

    private void DrawGizmo(Color color)
    {
        BuildingGridOverlay grid = FindAnyObjectByType<BuildingGridOverlay>();
        float cellSize = (grid != null && grid.CellSize > 0f) ? grid.CellSize : 0.5f;

        Vector3 center = transform.position;
        if (grid != null && grid.CellSize > 0f)
        {
            Vector2Int cell = grid.WorldToCell(center);
            center = grid.CellToWorldCenter(cell);
            center.y = grid.SurfaceY + 0.02f;
        }

        Gizmos.color = color;
        Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.02f, cellSize));

        // 반투명 채움
        Color fill = color; fill.a *= 0.3f;
        Gizmos.color = fill;
        Gizmos.DrawCube(center, new Vector3(cellSize, 0.01f, cellSize));
    }
#endif
}
