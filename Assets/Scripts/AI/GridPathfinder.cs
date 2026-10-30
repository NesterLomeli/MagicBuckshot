using System.Collections.Generic;
using UnityEngine;

// A* pathfinding so enemies can walk around walls and props. at the start it puts a grid over
// the level and marks which cells are blocked, FindPath gives back waypoints to follow
public class GridPathfinder : MonoBehaviour
{
    public static GridPathfinder Instance { get; private set; }

    [Tooltip("World size of the grid, centred on this object.")]
    [SerializeField] private Vector2 areaSize = new Vector2(40f, 30f);
    [SerializeField] private float cellSize = 0.5f;
    [Tooltip("Roughly the enemy collider radius. Keeps paths away from wall edges.")]
    [SerializeField] private float agentRadius = 0.3f;
    [Tooltip("Layers that block walking (Walls, Props).")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private bool drawGizmos = true;

    private int width, height;
    private Vector2 origin;
    private bool[] walkable;

    private float[] gCost;
    private int[] parent;
    // stamps let each search reuse the arrays without clearing them every time
    private int[] visitedStamp;
    private bool[] closed;
    private int currentStamp;
    private readonly List<(int node, float f)> openHeap = new List<(int, float)>();   // open list as a heap so the cheapest cell comes out first

    private static readonly int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };   // 8 directions, first 4 straight, last 4 diagonal
    private static readonly int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
    private const float Diagonal = 1.41421356f;

    private void Awake()
    {
        Instance = this;
        Build();
    }

    public void Build()
    {
        width = Mathf.Max(1, Mathf.CeilToInt(areaSize.x / cellSize));
        height = Mathf.Max(1, Mathf.CeilToInt(areaSize.y / cellSize));
        origin = (Vector2)transform.position - areaSize * 0.5f;

        int count = width * height;
        walkable = new bool[count];
        gCost = new float[count];
        parent = new int[count];
        visitedStamp = new int[count];
        closed = new bool[count];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                walkable[x + y * width] = CheckCell(x, y);
    }

    // recheck part of the grid, like after a barrel breaks and frees up space
    public void RefreshArea(Bounds area)
    {
        if (walkable == null) return;
        Vector2Int min = WorldToCell(area.min - Vector3.one * agentRadius);
        Vector2Int max = WorldToCell(area.max + Vector3.one * agentRadius);
        for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(height - 1, max.y); y++)
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(width - 1, max.x); x++)
                walkable[x + y * width] = CheckCell(x, y);
    }

    // a cell is walkable if an enemy sized circle there doesn't hit a wall or prop
    private bool CheckCell(int x, int y)
    {
        Collider2D hit = Physics2D.OverlapCircle(CellToWorld(x, y), agentRadius, obstacleMask);
        return hit == null || hit.isTrigger;
    }

    private Vector2 CellToWorld(int x, int y) =>
        origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

    private Vector2Int WorldToCell(Vector2 world) =>
        new Vector2Int(Mathf.FloorToInt((world.x - origin.x) / cellSize),
                       Mathf.FloorToInt((world.y - origin.y) / cellSize));

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    private bool IsWalkable(int x, int y) => InBounds(x, y) && walkable[x + y * width];

    // if a point is inside something solid, search outward for the closest open cell
    private bool TryGetNearestWalkable(Vector2Int cell, out Vector2Int result)
    {
        result = cell;
        if (IsWalkable(cell.x, cell.y)) return true;

        for (int r = 1; r <= 4; r++)
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    if (IsWalkable(cell.x + x, cell.y + y))
                    {
                        result = new Vector2Int(cell.x + x, cell.y + y);
                        return true;
                    }
                }
        return false;
    }

    // normal A*: always check the cell with the lowest (cost so far + guess to the goal) next
    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
    {
        if (walkable == null) return null;
        if (!TryGetNearestWalkable(WorldToCell(start), out Vector2Int s)) return null;
        if (!TryGetNearestWalkable(WorldToCell(goal), out Vector2Int g)) return null;

        int startIndex = s.x + s.y * width;
        int goalIndex = g.x + g.y * width;

        currentStamp++;
        openHeap.Clear();
        Visit(startIndex, 0f, -1);
        Push(startIndex, Heuristic(s, g));

        bool found = false;
        while (openHeap.Count > 0)
        {
            int current = Pop();
            if (closed[current]) continue;
            closed[current] = true;

            if (current == goalIndex) { found = true; break; }

            int cx = current % width, cy = current / width;
            for (int i = 0; i < 8; i++)
            {
                int nx = cx + dx[i], ny = cy + dy[i];
                if (!IsWalkable(nx, ny)) continue;

                bool diagonal = i >= 4;
                if (diagonal && (!IsWalkable(cx + dx[i], cy) || !IsWalkable(cx, cy + dy[i]))) continue;   // no cutting diagonally through wall corners

                int neighbour = nx + ny * width;
                float newCost = gCost[current] + (diagonal ? Diagonal : 1f);

                bool seen = visitedStamp[neighbour] == currentStamp;
                if (seen && (closed[neighbour] || newCost >= gCost[neighbour])) continue;

                Visit(neighbour, newCost, current);
                Push(neighbour, newCost + Heuristic(new Vector2Int(nx, ny), g));
            }
        }

        if (!found) return null;

        List<Vector2> path = new List<Vector2>();
        // follow the parent links back from the goal to build the path
        for (int n = goalIndex; n != -1; n = parent[n])
            path.Add(CellToWorld(n % width, n / width));
        path.Reverse();

        if (IsWalkable(WorldToCell(goal).x, WorldToCell(goal).y))
            path[path.Count - 1] = goal;

        return Smooth(start, path);
    }

    private void Visit(int index, float cost, int from)
    {
        if (visitedStamp[index] != currentStamp) closed[index] = false;
        visitedStamp[index] = currentStamp;
        gCost[index] = cost;
        parent[index] = from;
    }

    // distance guess that allows diagonal moves (octile distance)
    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int ddx = Mathf.Abs(a.x - b.x), ddy = Mathf.Abs(a.y - b.y);
        return (ddx + ddy) + (Diagonal - 2f) * Mathf.Min(ddx, ddy);
    }

    // skip waypoints you can reach in a straight line so enemies don't zigzag
    private List<Vector2> Smooth(Vector2 start, List<Vector2> path)
    {
        List<Vector2> result = new List<Vector2>();
        Vector2 anchor = start;
        int i = 0;
        while (i < path.Count)
        {
            int furthest = i;
            for (int j = i + 1; j < path.Count; j++)
            {
                if (HasClearance(anchor, path[j])) furthest = j;
                else break;
            }
            result.Add(path[furthest]);
            anchor = path[furthest];
            i = furthest + 1;
        }
        return result;
    }

    // true if an enemy could walk straight from a to b without hitting anything
    public bool HasClearance(Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float dist = delta.magnitude;
        if (dist < 0.001f) return true;
        RaycastHit2D hit = Physics2D.CircleCast(a, agentRadius * 0.9f, delta / dist, dist, obstacleMask);
        return hit.collider == null || hit.collider.isTrigger;
    }

    // heap add and remove, keeps the lowest cost on top
    private void Push(int node, float f)
    {
        openHeap.Add((node, f));
        int i = openHeap.Count - 1;
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (openHeap[p].f <= openHeap[i].f) break;
            (openHeap[p], openHeap[i]) = (openHeap[i], openHeap[p]);
            i = p;
        }
    }

    private int Pop()
    {
        int top = openHeap[0].node;
        int last = openHeap.Count - 1;
        openHeap[0] = openHeap[last];
        openHeap.RemoveAt(last);

        int i = 0;
        while (true)
        {
            int l = i * 2 + 1, r = l + 1, smallest = i;
            if (l < openHeap.Count && openHeap[l].f < openHeap[smallest].f) smallest = l;
            if (r < openHeap.Count && openHeap[r].f < openHeap[smallest].f) smallest = r;
            if (smallest == i) break;
            (openHeap[smallest], openHeap[i]) = (openHeap[i], openHeap[smallest]);
            i = smallest;
        }
        return top;
    }

    // editor only: draws the grid (green = walkable, red = blocked) when it's selected
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, areaSize);

        if (walkable == null) return;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Gizmos.color = walkable[x + y * width] ? new Color(0f, 1f, 0f, 0.15f) : new Color(1f, 0f, 0f, 0.35f);
                Gizmos.DrawCube(CellToWorld(x, y), Vector3.one * cellSize * 0.9f);
            }
    }
}
