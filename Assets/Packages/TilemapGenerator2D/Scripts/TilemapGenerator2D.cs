namespace DevelopersHub.ProceduralTilemapGenerator2D
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Tilemaps;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Mathematics;
    using ProceduralTilemapGenerator2D.Extensions;
    using UnityEngine.Rendering;
    
    public class TilemapGenerator2D : MonoBehaviour
    {
        
        private Grid grid = null;
        private List<TilemapData> tilemaps = new List<TilemapData>();
        [SerializeField] private bool _generateOnAwake = true;
        [SerializeField] private Type type = Type.Static;
        //[SerializeField] private bool multiFrame = true;
        [SerializeField] private float generationPeriod = 0.5f;
        [SerializeField] private int regenerateDistance = 1;
        // [SerializeField] private GenerateType _generationType = GenerateType.Async;
        // [SerializeField] private ClearType _clearType = ClearType.ClearPreviousAsync;
        [SerializeField] private int tilemapSize = 50;
        [SerializeField] private Vector3 staticPosition = Vector3.zero;
        [SerializeField] private Transform infiniteTarget = null;
        [SerializeField] private int offset = 5;
        [SerializeField] private Sprite baseTile = null;
        [SerializeField] private ColliderType baseCollider = ColliderType.Walkable;
        [SerializeField] private TilesetHeightData baseHeights = null;
        public List<TilemapObjectData> baseObjects = null;
        [SerializeField] private List<TilesetData> tilesets = null; public List<TilesetData> Tilesets { get { return tilesets; } }
        [SerializeField] private int placeTilesPerFrame = 50;
        [SerializeField] private int clearTilesPerFrame = 50;
        [SerializeField] private int playerSortLayer = 0;
        private Vector3Int lastCell = Vector3Int.zero;
        private float generationTimer = 0;
        
        // Directions for 8-connected neighbors (including diagonals)
        private static readonly int2[] eightDirections = new int2[]
        {
            new int2(0, 1),   // Up
            new int2(0, -1),  // Down
            new int2(-1, 0),  // Left
            new int2(1, 0),   // Right
            new int2(-1, 1),  // Up-Left
            new int2(1, 1),   // Up-Right
            new int2(-1, -1), // Down-Left
            new int2(1, -1)   // Down-Right
        };
        
        // Directions for 4-connected neighbors (excluding diagonals)
        private static readonly int2[] fourDirections = new int2[]
        {
            new int2(0, 1),   // Up
            new int2(0, -1),  // Down
            new int2(-1, 0),  // Left
            new int2(1, 0),   // Right
        };

        private static readonly int anyTile = 0;
        
        public enum Type
        {
            Static = 0, Infinite = 1
        }
        
        public enum ObjectType
        {
            Prefab = 0, Sprite = 1
        }
        
        public enum ColliderType
        {
            Walkable = 0, NonWalkable = 1
        }
        
        public enum Collider2DType
        {
            None = 0, Circle = 1, Box = 2
        }
        
        public class CellData
        {
            public int tilemap = 0;
            public int tileset = -1;
            public int rule = -1;
            public TilemapObject2D Object2DReference = null;
            public Collider2D collider = null;
            public float distance = float.PositiveInfinity;
        }
        
        public class TilemapData
        {
            public Tilemap tilemap = null;
            public TilemapRenderer renderer = null;
            public Dictionary<Vector3Int, CellData> toPlace = new Dictionary<Vector3Int, CellData>();
            public Dictionary<Vector3Int, CellData> placed = new Dictionary<Vector3Int, CellData>();
            public HashSet<Vector3Int> toClear = new HashSet<Vector3Int>();
        }
        
        [System.Serializable]
        public class TilesetData
        {
            public ColliderType collider = ColliderType.Walkable;
            public float colliderThickness = 0.05f;
            public int priority = 1;
            public RuleTile ruleTile = null;
            public NoiseGenerator2D noise = new NoiseGenerator2D();
            public NoiseGenerator2D.NoiseType noiseType = NoiseGenerator2D.NoiseType.Perlin;
            public int noiseSeed = 1234;
            public float noiseScale = 0.05f;
            public float threshold = 0.9f;
            public TilesetHeightData heights = null;
            public List<TilemapObjectData> objects = null;
        }
        
        [System.Serializable]
        public class TilesetHeightData
        {
            public RuleTile ruleTile = null;
            public float colliderThickness = 0.25f;
            public float colliderHorizontalPadding = 0.1f;
            public Sprite topSlope = null;
            public Sprite rightSlope = null;
            public Sprite leftSlope = null;
            public Sprite bottomSlope = null;
            public int slopeFrequency = 8;
            public NoiseGenerator2D noise = new NoiseGenerator2D();
            public NoiseGenerator2D.NoiseType noiseType = NoiseGenerator2D.NoiseType.Perlin;
            public int noiseSeed = 1234;
            public float noiseScale = 0.05f;
            public float threshold = 0.9f;
        }
        
        [System.Serializable]
        public class TilemapObjectData
        {
            public int priority = 1;
            public ObjectType type = ObjectType.Prefab;
            public TilemapObject2D[] prefabs = null;
            public Collider2DType colliderType = Collider2DType.None;
            public float colliderSize = 0.5f;
            public Sprite[] sprites = null;
            public float scale = 1f;
            public NoiseGenerator2D noise = new NoiseGenerator2D();
            public NoiseGenerator2D.NoiseType noiseType = NoiseGenerator2D.NoiseType.Perlin;
            public int noiseSeed = 1234;
            public float noiseScale = 0.05f;
            public float threshold = 0.9f;
            public bool coverHeights = true;
        }

        private struct RuleTileData
        {
            public int tileset;
            public int rule;
            public int neighbor;
            public int neighborValue;
            public int3 neighborPosition;
            public bool valid;
            public int maxRules;
            public RuleTileData(int t, int r, int m, int n, int x, int y, int z, bool v, int va)
            {
                tileset = t;
                rule = r;
                neighbor = n;
                neighborPosition = new int3(x, y, z);
                valid = v;
                maxRules = m;
                neighborValue = va;
            }
        }
        
        private struct AddData
        {
            public int tilemap;
            public int tileset;
            public int rule;
            public int3 cell;
            public AddData(int m, int t, int r, int x, int y, int z)
            {
                tilemap = m;
                tileset = t;
                rule = r;
                cell = new int3(x, y, z);
            }
        }
        
        private void Awake()
        {
            SetNoiseData();
            if (_generateOnAwake || type == Type.Infinite)
            {
                Generate(GenerateType.Immediate, ClearType.ClearPreviousImmediate);
            }
        }

        private void Start()
        {

        }
        
        private void Update()
        {
            if (tilemaps != null && tilemaps.Count == 4)
            {
                if (type == Type.Infinite && infiniteTarget != null)
                {
                    Vector3Int currentCell = tilemaps[0].tilemap.WorldToCell(infiniteTarget.position);
                    if (Vector3.Distance(currentCell, lastCell) >= regenerateDistance)
                    {
                        if (generationTimer <= 0)
                        {
                            generationTimer = generationPeriod;
                            Generate(GenerateType.Async, ClearType.ClearPreviousAsync);
                        }
                        else
                        {
                            generationTimer -= Time.deltaTime;
                        }
                    }
                }
                
                int remained = placeTilesPerFrame;
                int share = Mathf.RoundToInt(remained * 0.1f);
                
                if (remained > 0 && tilemaps[1].toPlace.Count > 0)
                {
                    int count = PlaceTilesForTilemap(1, share);
                    remained -= count;
                }
                if (remained > 0 && tilemaps[2].toPlace.Count > 0)
                {
                    int count = PlaceTilesForTilemap(2, share);
                    remained -= count;
                }
                if (remained > 0 && tilemaps[3].toPlace.Count > 0)
                {
                    int count = PlaceTilesForTilemap(3, share);
                    remained -= count;
                }
                if (remained > 0 && tilemaps[0].toPlace.Count > 0)
                {
                    PlaceTilesForTilemap(0, remained);
                }
                
                remained = clearTilesPerFrame;
                if (remained > 0 && tilemaps[1].toClear.Count > 0)
                {
                    int count = ClearTilesForTilemap(1, share);
                    remained -= count;
                }
                if (remained > 0 && tilemaps[2].toClear.Count > 0)
                {
                    int count = ClearTilesForTilemap(2, share);
                    remained -= count;
                }
                if (remained > 0 && tilemaps[3].toClear.Count > 0)
                {
                    int count = ClearTilesForTilemap(3, share);
                    remained -= count;
                }
                if (remained > 0 && tilemaps[0].toClear.Count > 0)
                {
                    ClearTilesForTilemap(0, remained);
                }
            }
        }

        private void OnDestroy()
        {
            
        }

        private int PlaceTilesForTilemap(int tilemap, int count)
        {
            if (tilemaps[tilemap].toPlace.Count > 0)
            {
                int n = Mathf.Min(tilemaps[tilemap].toPlace.Count, count);
                var tiles = GetClosestToPlace(tilemap, n);
                for (int i = 0; i < tiles.Length; i++)
                {
                    Place(tiles[i].Key, tiles[i].Value);
                }
                count -= n;
            }
            return count;
        }
        
        private int ClearTilesForTilemap(int tilemap, int count)
        {
            Vector3Int[] cells = new Vector3Int[Mathf.Min(tilemaps[tilemap].toClear.Count, count)];
            int i = 0;
            foreach (var cell in tilemaps[tilemap].toClear)
            {
                cells[i] = cell;
                i++;
                if (i >= cells.Length) { break; }
            }
            for (int j = 0; j < cells.Length; j++)
            {
                Vector3Int cell = cells[j];
                if (tilemaps[tilemap].placed.TryGetValue(cell, out var data))
                {
                    if (data.Object2DReference != null)
                    {
                        Destroy(data.Object2DReference.gameObject);
                    }
                    if (data.collider != null)
                    {
                        Destroy(data.collider.gameObject);
                    }
                    tilemaps[tilemap].placed.Remove(cell);
                }
                tilemaps[tilemap].tilemap.SetTile(cell, null);
                tilemaps[tilemap].toClear.Remove(cell);
                count--;
            }
            return count;
        }
        
        private KeyValuePair<Vector3Int, CellData>[] GetClosestToPlace(int tilemap, int n)
        {
            if (tilemaps[tilemap].toPlace.Count == 0)
            {
                Debug.LogError("The dictionary is empty.");
            }
            if (n <= 0 || n > tilemaps[tilemap].toPlace.Count)
            {
                Debug.LogError("n=" + n + " but must be greater than 0 and less than or equal to the number of elements in the dictionary.");
            }

            // Create a max-heap to store the n closest entries
            var maxHeap = new MaxHeap(n);

            // Iterate through the dictionary
            foreach (var kvp in tilemaps[tilemap].toPlace)
            {
                if (maxHeap.Count < n)
                {
                    // If the heap is not full, add the entry
                    maxHeap.Insert(kvp);
                }
                else if (kvp.Value.distance < maxHeap.Peek().Value.distance)
                {
                    // If the current entry is closer than the farthest in the heap, replace it
                    maxHeap.ExtractMax();
                    maxHeap.Insert(kvp);
                }
            }

            // Extract the n closest entries from the heap
            var closestEntries = new KeyValuePair<Vector3Int, CellData>[n];
            for (int i = n - 1; i >= 0; i--)
            {
                closestEntries[i] = maxHeap.ExtractMax();
            }
            return closestEntries;
        }
        
        private class MaxHeap
        {
            private readonly List<KeyValuePair<Vector3Int, CellData>> _heap;
            private readonly int _capacity;
            public int Count => _heap.Count;

            public MaxHeap(int capacity)
            {
                _capacity = capacity;
                _heap = new List<KeyValuePair<Vector3Int, CellData>>(capacity);
            }

            public void Insert(KeyValuePair<Vector3Int, CellData> entry)
            {
                if (_heap.Count == _capacity)
                {
                    throw new InvalidOperationException("Heap is at full capacity.");
                }
                _heap.Add(entry);
                HeapifyUp(_heap.Count - 1);
            }

            public KeyValuePair<Vector3Int, CellData> ExtractMax()
            {
                if (_heap.Count == 0)
                {
                    throw new InvalidOperationException("Heap is empty.");
                }
                var max = _heap[0];
                _heap[0] = _heap[_heap.Count - 1];
                _heap.RemoveAt(_heap.Count - 1);
                HeapifyDown(0);
                return max;
            }

            public KeyValuePair<Vector3Int, CellData> Peek()
            {
                if (_heap.Count == 0)
                {
                    throw new InvalidOperationException("Heap is empty.");
                }
                return _heap[0];
            }

            private void HeapifyUp(int index)
            {
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;
                    if (_heap[parentIndex].Value.distance >= _heap[index].Value.distance)
                    {
                        break;
                    }
                    Swap(parentIndex, index);
                    index = parentIndex;
                }
            }

            private void HeapifyDown(int index)
            {
                while (true)
                {
                    int leftChildIndex = 2 * index + 1;
                    int rightChildIndex = 2 * index + 2;
                    int largestIndex = index;
                    if (leftChildIndex < _heap.Count && _heap[leftChildIndex].Value.distance > _heap[largestIndex].Value.distance)
                    {
                        largestIndex = leftChildIndex;
                    }
                    if (rightChildIndex < _heap.Count && _heap[rightChildIndex].Value.distance > _heap[largestIndex].Value.distance)
                    {
                        largestIndex = rightChildIndex;
                    }
                    if (largestIndex == index)
                    {
                        break;
                    }
                    Swap(index, largestIndex);
                    index = largestIndex;
                }
            }

            private void Swap(int i, int j)
            {
                var temp = _heap[i];
                _heap[i] = _heap[j];
                _heap[j] = temp;
            }
        }
        
        private void Clear(ClearType clear)
        {
            if (tilemaps != null && clear != ClearType.DonNotClear)
            {
                for (int i = 0; i < tilemaps.Count; i++)
                {
                    if (tilemaps[i] != null && tilemaps[i].tilemap != null)
                    {
                        tilemaps[i].toPlace.Clear();
                        if (clear == ClearType.ClearPreviousImmediate)
                        {
                            tilemaps[i].toClear.Clear();
                            tilemaps[i].tilemap.ClearAllTiles();
                            foreach (var cell in tilemaps[i].placed)
                            {
                                if (cell.Value != null)
                                {
                                    if (cell.Value.Object2DReference != null)
                                    {
                                        #if UNITY_EDITOR
                                        if (Application.isPlaying)
                                        {
                                            Destroy(cell.Value.Object2DReference.gameObject);
                                        }
                                        else
                                        {
                                            DestroyImmediate(cell.Value.Object2DReference.gameObject);
                                        }
                                        #else
                                        Destroy(cell.Value.objectReference.gameObject);
                                        #endif
                                    }
                                    if (cell.Value.collider != null)
                                    {
                                        #if UNITY_EDITOR
                                        if (Application.isPlaying)
                                        {
                                            Destroy(cell.Value.collider.gameObject);
                                        }
                                        else
                                        {
                                            DestroyImmediate(cell.Value.collider.gameObject);
                                        }
                                        #else
                                        Destroy(cell.Value.collider.gameObject);
                                        #endif
                                    }
                                }
                            }
                            TilemapObject2D[] objs = tilemaps[i].tilemap.gameObject.GetComponentsInChildren<TilemapObject2D>();
                            if (objs != null)
                            {
                                foreach (var cell in objs)
                                {
                                    #if UNITY_EDITOR
                                    if (Application.isPlaying)
                                    {
                                        Destroy(cell.gameObject);
                                    }
                                    else
                                    {
                                        DestroyImmediate(cell.gameObject);
                                    }
                                    #else
                                    Destroy(cell.gameObject);
                                    #endif
                                }
                            }
                            if (i == 0 || i == 2)
                            {
                                Collider2D[] cols = tilemaps[i].tilemap.gameObject.GetComponentsInChildren<Collider2D>();
                                if (cols != null)
                                {
                                    foreach (var col in cols)
                                    {
                                        if (col.gameObject != tilemaps[i].tilemap.gameObject)
                                        {
                                            #if UNITY_EDITOR
                                            if (Application.isPlaying)
                                            {
                                                Destroy(col.gameObject);
                                            }
                                            else
                                            {
                                                DestroyImmediate(col.gameObject);
                                            }
                                            #else
                                            Destroy(col.gameObject);
                                            #endif
                                        }
                                    }
                                }
                            }
                            tilemaps[i].placed.Clear();
                        }
                        else if (clear == ClearType.ClearPreviousAsync)
                        {
                            foreach (var cell in tilemaps[i].placed)
                            {
                                if (cell.Value != null)
                                {
                                    tilemaps[i].toClear.Add(cell.Key);
                                }
                            }
                        }
                    }
                }
            }
        }

        public enum GenerateType
        {
            Immediate = 0, Async = 1
        }
        
        public enum ClearType
        {
            DonNotClear = 0, ClearPreviousImmediate = 1, ClearPreviousAsync = 2
        }
        
        public void Generate(GenerateType speed, ClearType clear)
        {
            CreateTilemapsIfNotExists();
            Vector3Int target = type == Type.Static ? tilemaps[0].tilemap.WorldToCell(staticPosition) : infiniteTarget != null ? tilemaps[0].tilemap.WorldToCell(infiniteTarget.position) : Vector3Int.zero;
            target.x -= Mathf.FloorToInt(tilemapSize / 2f);
            target.y -= Mathf.FloorToInt(tilemapSize / 2f);
            Generate(target, tilemapSize, speed, clear);
        }
        
        public void Generate(Vector3 centerWorldPosition, int size, GenerateType speed, ClearType clear)
        {
            CreateTilemapsIfNotExists();
            Vector3Int target = tilemaps[0].tilemap.WorldToCell(centerWorldPosition);
            target.x -= Mathf.FloorToInt(size / 2f);
            target.y -= Mathf.FloorToInt(size / 2f);
            Generate(target, size, speed, clear);
        }

        public void Generate(Vector3Int bottomLeftCell, int size, GenerateType speed, ClearType clear)
        {
            // ToDo: Remove repeated native array allocation and allocate only once if possible instead of once per tile or action
            bool jobsActive = true;
            if (size <= 0) { size = 1; }
            CreateTilemapsIfNotExists();
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                clear = ClearType.ClearPreviousImmediate;
                speed = GenerateType.Immediate;
            }
            #endif
            Clear(clear);
            int2 distanceCell = int2.zero;
            if (type == Type.Infinite && infiniteTarget != null)
            {
                var dc = tilemaps[0].tilemap.WorldToCell(infiniteTarget.position);
                distanceCell.x = dc.x;
                distanceCell.y = dc.y;
            }
            else
            {
                var dc = tilemaps[0].tilemap.WorldToCell(staticPosition);
                distanceCell.x = dc.x;
                distanceCell.y = dc.y;
            }
            int searchRange = offset - 2;
            if (tilesets != null && tilesets.Count > 0)
            {
                tilesets.Sort((a, b) => b.priority.CompareTo(a.priority));
            }
            int s = size + offset * 2;
            NativeArray<int> occupied = new NativeArray<int>(s * s, Allocator.TempJob);
            NativeArray<int> occupiedRegions = new NativeArray<int>(s * s, Allocator.TempJob);
            NativeArray<int> occupiedTransitions = new NativeArray<int>(s * s, Allocator.TempJob);
            occupied.Fill(-1);
            occupiedTransitions.Fill(-1);
            
            int rulesCount = SetBaseNoiseValues(occupied, s, bottomLeftCell);
            MakeSureThereIsOneTilesSpaceBetweenSpots(occupied, occupiedRegions, s, searchRange);
            MakeSureThereIsTwoTilesSpaceBetweenSpots(occupied, occupiedRegions, s, searchRange, 0, occupied.Length - 1);
            
            #region Fill tiles with 3 direct neighbors
            for (int i = 0; i < occupied.Length; i++)
            {
                if (occupied[i] >= 0) { continue; }
                int x = i % s;
                int y = i / s;
                if (x < offset || y < offset || x >= (s - offset) || y >= (s - offset)) { continue; }
                int n1 = -1;
                int count = 0;
                foreach (var dir in fourDirections)
                {
                    int n2 = occupied[(x + dir.x) + (y + dir.y) * s];
                    if (n2 >= 0) { count++; n1 = n2; }
                    if (count >= 3) { occupied[x + y * s] = n1; break; }
                }
            }
            #endregion
            
            #region Determine base tiles transitions and apply all base tiles
            var rules = new NativeArray<RuleTileData>(rulesCount, Allocator.TempJob);
            int u = 0;
            for (int i = 0; i < tilesets.Count; i++)
            {
                if (tilesets[i].ruleTile != null)
                {
                    int mr = tilesets[i].ruleTile.m_TilingRules.Count;
                    for (int j = 0; j < mr; j++)
                    {
                        int nc = tilesets[i].ruleTile.m_TilingRules[j].m_Neighbors.Count;
                        for (int l = 0; l < tilesets[i].ruleTile.m_TilingRules[j].m_NeighborPositions.Count; l++)
                        {
                            var m = tilesets[i].ruleTile.m_TilingRules[j];
                            var p = m.m_NeighborPositions[l];
                            bool v = m.m_Sprites != null && m.m_Sprites.Length > 0;
                            int va = l >= nc ? anyTile : tilesets[i].ruleTile.m_TilingRules[j].m_Neighbors[l];
                            rules[u] = new RuleTileData(i, j, mr, l, p.x, p.y, p.z, v, va);
                            u++;
                        }
                    }
                }
            }
            NativeArray<AddData> addedCells = new NativeArray<AddData>(occupied.Length, Allocator.TempJob);
            addedCells.Fill(new AddData(-1, -1, -1, -1, -1, -1));
            new AddBaseTilesJob()
            {
                s = s,
                offset = offset,
                bottomLeftCell = new int2(bottomLeftCell.x, bottomLeftCell.y),
                occupied = occupied,
                anyTile = anyTile,
                rules = rules,
                occupiedTransitions = occupiedTransitions,
                addedCells = addedCells
            }.Schedule(occupied.Length, 64).Complete();
            for (int i = 0; i < addedCells.Length; i++)
            {
                AddData data = addedCells[i];
                if (data.tilemap < 0) { continue; }
                Vector3Int cell = new Vector3Int(data.cell.x, data.cell.y, data.cell.z);
                Add(cell, data.tilemap, data.tileset, data.rule, speed, distanceCell);
            }
            addedCells.Dispose();
            #endregion
            
            #region Set occupied heights based on cliffs noise
            NativeArray<int> occupiedHeights = new NativeArray<int>(s * s, Allocator.TempJob);
            NativeArray<int> occupiedHeightsTransitions = new NativeArray<int>(s * s, Allocator.Temp);
            NativeArray<int> occupiedHeightsSlopes = new NativeArray<int>(s * s, Allocator.TempJob);
            occupiedHeightsSlopes.Fill(-1);
            occupiedHeights.Fill(-2);
            occupiedHeightsTransitions.Fill(-2);
            int of = 1;
            for (int i = 0; i < occupied.Length; i++)
            {
                int x = i % s;
                int y = i / s;
                if (x < of || y < of || x >= (s - of) || y >= (s - of)) { continue; }
                int n = occupied[i];
                if (n >= 0)
                {
                    var h = tilesets[occupied[i]].heights;
                    if (h != null && h.ruleTile != null)
                    {
                        float noiseValue = h.noise.GetNoise(bottomLeftCell.x + x - offset, y + bottomLeftCell.y - offset);
                        float normalizedNoise = (noiseValue + 1f) * 0.5f;
                        if (normalizedNoise >= h.threshold)
                        {
                            bool valid = true;
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var dir = eightDirections[j];
                                if (occupied[(x + dir.x) + (y + dir.y) * s] != n) { valid = false; break; }
                            }
                            if (valid) { occupiedHeights[x + y * s] = n; }
                        }
                    }
                }
                else
                {
                    if (baseHeights != null && baseHeights.ruleTile != null)
                    {
                        float noiseValue = baseHeights.noise.GetNoise(bottomLeftCell.x + x - offset, y + bottomLeftCell.y - offset);
                        float normalizedNoise = (noiseValue + 1f) * 0.5f;
                        if (normalizedNoise >= baseHeights.threshold)
                        {
                            bool valid = true;
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var dir = eightDirections[j];
                                if (dir.x == 0 && dir.y == 0) { continue; }
                                if (occupied[(x + dir.x) + (y + dir.y) * s] != -1) { valid = false; break; }
                            }
                            if (valid) { occupiedHeights[x + y * s] = -1; }
                        }
                    }
                }
            }
            #endregion
            
            #region Fill tiles with 3 direct neighbors from cliffs
            for (int i = 0; i < occupiedHeights.Length; i++)
            {
                int n = occupiedHeights[i];
                if (n != -2) { continue; }
                int x = i % s;
                int y = i / s;
                if (x < of || y < of || x >= (s - of) || y >= (s - of)) { continue; }
                int n1 = -2;
                int count = 0;
                foreach (var dir in fourDirections)
                {
                    int n2 = occupiedHeights[(x + dir.x) + (y + dir.y) * s];
                    if (n2 >= -1) { count++; n1 = n2; }
                    if (count >= 3) { occupiedHeights[x + y * s] = n1; break; }
                }
            }
            #endregion
            
            #region Remove thin tile areas from cliffs
            NativeArray<bool> processed = new NativeArray<bool>(s * s, Allocator.TempJob);
            for (int i = 0; i < occupiedHeights.Length; i++)
            {
                int n = occupiedHeights[i];
                if (n < -1 || processed[i]) { continue; }
                int x = i % s;
                int y = i / s;
                if (x < of || y < of || x >= (s - of) || y >= (s - of)) { continue; }
                NativeArray<bool> region = new NativeArray<bool>(s * s, Allocator.Temp);
                FloodFill(x, y, s, n, occupiedHeights, region, processed);
                int c = 1;
                int count = 0;
                while (c  > 0)
                {
                    c = 0;
                    count = 0;
                    for (int j = 0; j < region.Length; j++)
                    {
                        if (!region[j]) { continue; }
                        int x1 = j % s;
                        int y1 = j / s;
                        if (x1 < of || y1 < of || x1 >= (s - of) || y1 >= (s - of)) { continue; }
                        bool t = occupiedHeights[(x1 + 0) + (y1 + 1) * s] == n;
                        bool b = occupiedHeights[(x1 + 0) + (y1 - 1) * s] == n;
                        bool r = occupiedHeights[(x1 + 1) + (y1 + 0) * s] == n;
                        bool l = occupiedHeights[(x1 - 1) + (y1 + 0) * s] == n;
                        bool tr = occupiedHeights[(x1 + 1) + (y1 + 1) * s] == n;
                        bool tl = occupiedHeights[(x1 - 1) + (y1 + 1) * s] == n;
                        bool br = occupiedHeights[(x1 + 1) + (y1 - 1) * s] == n;
                        bool bl = occupiedHeights[(x1 - 1) + (y1 - 1) * s] == n;
                        if (!((t && tl && l) || (t && tr && r) || (b && bl && l) || (b && br && r)) || (tr && bl && !tl && !br && t && l && r && b) || (!tr && !bl && tl && br && t && l && r && b))
                        {
                            c++;
                            occupiedHeights[x1 + y1 * s] = -2;
                            region[j] = false;
                        }
                        else
                        {
                            count++;
                        }
                    }
                }
                int fr = 5;
                if (n >= 0)
                {
                    fr = tilesets[n].heights.slopeFrequency;
                }
                else
                {
                    fr = baseHeights.slopeFrequency;
                }

                for (int j = 0; j < region.Length; j++)
                {
                    if (!region[j]) { continue; }
                    int x1 = j % s;
                    int y1 = j / s;
                    if (x1 < of || y1 < of || x1 >= (s - of) || y1 >= (s - of)) { continue; }
                    Vector3Int cell = new Vector3Int(bottomLeftCell.x + x1 - offset, bottomLeftCell.y + y1 - offset, 0);
                    if (ShouldPlaceSlope(cell.x, cell.y, fr))
                    {
                        int cn = 0;
                        bool tp = false, lf = false, rt = false, dn = false;
                        for (int k = 0; k < eightDirections.Length; k++)
                        {
                            var dir = eightDirections[k];
                            if (region[(x1 + dir.x) + (y1 + dir.y) * s])
                            {
                                cn++;
                                if (dir.x == 1 && dir.y == 0) { rt = true; }
                                else if (dir.x == -1 && dir.y == 0) { lf = true; }
                                else if (dir.x == 0 && dir.y == 1) { tp = true; }
                                else if (dir.x == 0 && dir.y == -1) { dn = true; }
                            }
                        }
                        if (cn != eightDirections.Length)
                        {
                            if (tp && rt && lf && !dn)
                            {
                                // Slope down
                                occupiedHeightsSlopes[j] = 6;
                            }
                            else if (!tp && rt && lf && dn)
                            {
                                // Slope top
                                occupiedHeightsSlopes[j] = 7;
                            }
                            else if (tp && !rt && lf && dn)
                            {
                                // Slope right
                                occupiedHeightsSlopes[j] = 8;
                            }
                            else if (tp && rt && !lf && dn)
                            {
                                // Slope left
                                occupiedHeightsSlopes[j] = 9;
                            }
                        }
                    }
                }
                region.Dispose();
            }
            processed.Dispose();
            #endregion

            #region Determine cliff tiles
            for (int i = 0; i < occupiedHeights.Length; i++)
            {
                int n = occupiedHeights[i];
                if (n < -1) { continue; }
                int x = i % s;
                int y = i / s;
                if (x < offset || y < offset || x >= (s - offset) || y >= (s - offset)) { continue; }
                Vector3Int cell = new Vector3Int(bottomLeftCell.x + x - offset, bottomLeftCell.y + y - offset, 0);
                int sl = occupiedHeightsSlopes[i];
                int slr = 0;
                switch (sl)
                {
                    case 6:
                        if ((n >= 0 && tilesets[n].heights.bottomSlope != null) || (n == -1 && baseHeights.bottomSlope != null))
                        {
                            slr = -6;
                        }
                        break;
                    case 7:
                        if ((n >= 0 && tilesets[n].heights.topSlope != null) || (n == -1 && baseHeights.topSlope != null))
                        {
                            slr = -7;
                        }
                        break;
                    case 8:
                        if ((n >= 0 && tilesets[n].heights.rightSlope != null) || (n == -1 && baseHeights.rightSlope != null))
                        {
                            slr = -8;
                        }
                        break;
                    case 9:
                        if ((n >= 0 && tilesets[n].heights.leftSlope != null) || (n == -1 && baseHeights.leftSlope != null))
                        {
                            slr = -9;
                        }
                        break;
                }
                if (slr != 0)
                {
                    occupiedHeightsTransitions[i] = sl;
                    Add(cell, 2, n, slr, speed, distanceCell);
                }
                else
                {
                    bool tp = false, lf = false, rt = false, dn = false, tpr = false, tpl = false, dnr = false, dnl = false;
                    Dictionary<int2, int> neighborDict = new Dictionary<int2, int>();
                    int count = 0;
                    for (int j = 0; j < eightDirections.Length; j++)
                    {
                        var dir = eightDirections[j];
                        int t = occupiedHeights[(x + dir.x) + (y + dir.y) * s];
                        if (t == n)
                        {
                            count++;
                            if (dir.x == 1 && dir.y == 0) { rt = true; }
                            else if (dir.x == -1 && dir.y == 0) { lf = true; }
                            else if (dir.x == 0 && dir.y == 1) { tp = true; }
                            else if (dir.x == 0 && dir.y == -1) { dn = true; }
                            else if (dir.x == 1 && dir.y == 1) { tpr = true; }
                            else if (dir.x == -1 && dir.y == 1) { tpl = true; }
                            else if (dir.x == 1 && dir.y == -1) { dnr = true; }
                            else if (dir.x == -1 && dir.y == -1) { dnl = true; }
                        }
                    }
                    if (count != eightDirections.Length)
                    {
                        occupiedHeightsTransitions[i] = n;
                        RuleTile ruleTile = null;
                        if (n >= 0) { ruleTile = tilesets[n].heights.ruleTile; }
                        else { ruleTile = baseHeights.ruleTile; } 
                        //if (count >= 3 && count <= 5 && tp && rt && !lf && !dn)
                        //if (count >= 3 && count <= 5 && tp && rt && !dn && (!lf || (lf && !tpl)))
                        if (count >= 3 && count <= 5 && tp && rt && ((!dn && (!lf || (lf && !tpl))) || (!lf && (!dn || (dn && !dnr))))) // In event of any issue change last dnr to tpl
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == 1 && p.y == 1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (!p.Equals(new int2(1, 0)) && !p.Equals(new int2(0, 1))) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else { neighborDict[p] = anyTile; }
                            }
                        }
                        //else if (count >= 3 && count <= 5 && tp && lf && !rt && !dn)
                        //else if (count >= 3 && count <= 5 && tp && lf && !rt && (!dn || (dn && !dnl)))
                        else if (count >= 3 && count <= 5 && tp && lf && ((!rt && (!dn || (dn && !dnl))) || (!dn && (!rt || (rt && !tpr)))))
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == -1 && p.y == 1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (!p.Equals(new int2(-1, 0)) && !p.Equals(new int2(0, 1))) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else { neighborDict[p] = anyTile; }
                            }
                        }
                        //else if (count >= 3 && count <= 5 && dn && rt && !tp && !lf)
                        //else if (count >= 3 && count <= 5 && dn && rt && !lf && (!tp || (tp && !tpr)))
                        else if (count >= 3 && count <= 5 && dn && rt && ((!lf && (!tp || (tp && !tpr))) || (!tp && (!lf || (lf && !dnl)))))
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == 1 && p.y == -1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (!p.Equals(new int2(1, 0)) && !p.Equals(new int2(0, -1))) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else { neighborDict[p] = anyTile; }
                            }
                        }
                        //else if (count >= 3 && count <= 5 && dn && lf && !tp && !rt)
                        //else if (count >= 3 && count <= 5 && dn && lf && !tp && (!rt || (rt && !dnr)))
                        else if (count >= 3 && count <= 5 && dn && lf && ((!tp && (!rt || (rt && !dnr))) || (!rt && (!tp || (tp && !tpl))))) // In event of any issue change last tpl to dnr
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == -1 && p.y == -1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (!p.Equals(new int2(-1, 0)) && !p.Equals(new int2(0, -1))) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else { neighborDict[p] = anyTile; }
                            }
                        }
                        else if (count == 7 && !tpl)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == -1 && p.y == 1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(-1, 0)) || p.Equals(new int2(0, 1))) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                            }
                        }
                        else if (count == 7 && !dnr)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == 1 && p.y == -1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(1, 0)) || p.Equals(new int2(0, -1))) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                            }
                        }
                        else if (count == 7 && !dnl)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == -1 && p.y == -1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(-1, 0)) || p.Equals(new int2(0, -1))) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                            }
                        }
                        else if (count == 7 && !tpr)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == 1 && p.y == 1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(1, 0)) || p.Equals(new int2(0, 1))) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                            }
                        }
                        else if (!dn && rt && lf)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.y == 1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (p.y == 0) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                            }
                        }
                        else if (!tp && rt && lf)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.y == -1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (p.y == 0) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                            }
                        }
                        else if (!lf && tp && dn)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == 1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (p.x == 0) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                            }
                        }
                        else if (!rt && tp && dn)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                if (p.x == -1) { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.This; }
                                else if (p.x == 0) { neighborDict[p] = anyTile; }
                                else { neighborDict[p] = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                            }
                        }
                        else
                        {

                        }
                        
                        // Variables to track the best matching rule
                        RuleTile.TilingRule bestRule = null;
                        int bestRuleIndex = -1;
                        int bestScore = -999;

                        // Iterate through all rules to find the closest match
                        for (int k = 0; k < ruleTile.m_TilingRules.Count; k++)
                        {
                            var rule = ruleTile.m_TilingRules[k];
                            if (rule == null) { continue; }
                            int score = 0;

                            // Compare each neighbor position in the rule with the current neighbor configuration
                            for (int j = 0; j < rule.m_NeighborPositions.Count; j++)
                            {
                                var p = rule.m_NeighborPositions[j];
                                if (neighborDict.TryGetValue(new int2(p.x, p.y), out int neighborState))
                                {
                                    int ne = j >= rule.m_Neighbors.Count ? anyTile : rule.m_Neighbors[j];
                                    if (neighborState == ne) { score++; }
                                    else if (ne != anyTile) { score--; }
                                }
                            }
                            if (score > bestScore) { bestScore = score; bestRule = rule; bestRuleIndex = k; }
                        }

                        // Set the tile based on the best matching rule
                        if (bestRule != null && bestRule.m_Sprites != null && bestRule.m_Sprites.Length > 0)
                        {
                            // Use the first sprite from the best matching rule (or apply randomization if needed)
                            Add(cell, 2, n, bestRuleIndex, speed, distanceCell);
                        }
                        else
                        {
                            // Fallback to the default sprite of the RuleTile
                            Add(cell, 2, n, -1, speed, distanceCell);
                        }
                    }
                    else
                    {
                        Add(cell, 2, n, -1, speed, distanceCell);
                    }
                }
            }
            #endregion
            
            #region Collect objects data from base objects and tilesets
            List<(int, int, TilemapObjectData)> objectsData = new List<(int, int, TilemapObjectData)>();
            if (baseObjects != null)
            {
                for (int i = 0; i < baseObjects.Count; i++)
                {
                    var obj = baseObjects[i];
                    if (obj != null && ((obj.type == ObjectType.Prefab && obj.prefabs != null && obj.prefabs.Length > 0) || (obj.type == ObjectType.Sprite && obj.sprites != null && obj.sprites.Length > 0)))
                    {
                        objectsData.Add((-1, i, obj));
                    }
                }
            }
            for (int i = 0; i < tilesets.Count; i++)
            {
                if (tilesets[i].objects != null)
                {
                    for (int j = 0; j < tilesets[i].objects.Count; j++)
                    {
                        var obj = tilesets[i].objects[j];
                        if (obj != null && ((obj.type == ObjectType.Prefab && obj.prefabs != null && obj.prefabs.Length > 0) || (obj.type == ObjectType.Sprite && obj.sprites != null && obj.sprites.Length > 0)))
                        {
                            objectsData.Add((i, j, obj));
                        }
                    }
                }
            }
            #endregion
            
            #region Place objects based on their prioraty
            if (objectsData.Count > 0)
            {
                objectsData.Sort((a, b) => b.Item3.priority.CompareTo(a.Item3.priority));
                NativeArray<int3> objectsOnBase = new NativeArray<int3>(s * s, Allocator.Temp);
                objectsOnBase.Fill(new int3(-2, -2, 0));
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        var a = objectsOnBase[x + y * s];
                        if (a.x != -2 || a.y != -2 || occupiedTransitions[x + y * s] >= 0 || occupiedHeightsTransitions[x + y * s] != -2) { continue; }
                        if (x < offset || y < offset || x >= (s - offset) || y >= (s - offset)) { continue; }
                        var oc = occupied[x + y * s];
                        for (int j = 0; j < objectsData.Count; j++)
                        {
                            var obj = objectsData[j].Item3;
                            if (oc != objectsData[j].Item1) { continue; }
                            float noiseValue = obj.noise.GetNoise(bottomLeftCell.x + x - offset, y + bottomLeftCell.y - offset);
                            float normalizedNoise = (noiseValue + 1f) * 0.5f;
                            if (normalizedNoise >= obj.threshold) { a = new int3(objectsData[j].Item1, objectsData[j].Item2, obj.coverHeights ? 1 : 0); break; }
                        }
                        if (a.x != -2 || a.y != -2)
                        {
                            int t = -1;
                            objectsOnBase[x + y * s] = a;
                            if (occupiedHeights[x + y * s] >= -1)
                            {
                                if (a.z == 1)
                                {
                                    // Put on tilemap 3
                                    t = 3;
                                }
                            }
                            else
                            {
                                // Put on tilemap 1
                                t = 1;
                            }
                            if (t >= 0)
                            {
                                Vector3Int cell = new Vector3Int(bottomLeftCell.x + x - offset, bottomLeftCell.y + y - offset, 0);
                                Add(cell, t, a.x, a.y, speed, distanceCell);
                            }
                        }
                    }
                }
                objectsOnBase.Dispose();
            }
            #endregion
            
            occupiedRegions.Dispose();
            rules.Dispose();
            occupiedHeightsSlopes.Dispose();
            occupiedHeights.Dispose();
            occupied.Dispose();
            occupiedTransitions.Dispose();
            if (type == Type.Infinite && infiniteTarget != null)
            {
                SetDistances();
            }
        }

        private int SetBaseNoiseValues(NativeArray<int> occupied, int s, Vector3Int bottomLeftCell)
        {
            int rulesCount = 0;
            for (int i = 0; i < tilesets.Count; i++)
            {
                var data = tilesets[i];
                if (data.ruleTile == null) { continue; }
                for (int j = 0; j < data.ruleTile.m_TilingRules.Count; j++)
                {
                    rulesCount += data.ruleTile.m_TilingRules[i].m_NeighborPositions.Count;
                }
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        if (occupied[x + y * s] >= 0) { continue; }
                        float noiseValue = data.noise.GetNoise(bottomLeftCell.x + x - offset, y + bottomLeftCell.y - offset);
                        float normalizedNoise = (noiseValue + 1f) * 0.5f;
                        if (normalizedNoise >= data.threshold) { occupied[x + y * s] = i; }
                    }
                }
            }
            return rulesCount;
        }
        
        private void MakeSureThereIsOneTilesSpaceBetweenSpots(NativeArray<int> occupied, NativeArray<int> regions, int s, int searchRange)
        {
            int ri = 1;
            for (int i = 0; i < occupied.Length; i++)
            {
                if (occupied[i] < 0) { continue; }
                int x = i % s;
                int y = i / s;
                if (x < (offset - searchRange) || y < (offset - searchRange) || x >= (s - offset + searchRange) || y >= (s - offset + searchRange)) { continue; }
                int r = regions[i];
                List<(int, int, int)> fr = new List<(int, int, int)>();
                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        if (j == 0 && k == 0)
                        { continue; }
                        int x1 = x + j;
                        int y1 = y + k;
                        int n = occupied[x + y * s];
                        int n1 = occupied[x1 + y1 * s];
                        if (r <= 0)
                        {
                            int r1 = regions[x1 + y1 * s];
                            if (r1 > 0)
                            {
                                fr.Add((x1, y1, r1));
                            }
                        }
                        if (n == n1) { continue; }
                        if (n1 >= 0)
                        {
                            if (n > n1) { occupied[x + y * s] = -1; }
                            else if (n < n1) { occupied[x1 + y1 * s] = -1; }
                            else { int2 t = PositionTieBreaker(new int2(x, y), new int2(x1, y1)); occupied[t.x + t.y * s] = -1; }
                        }
                    }
                }
                if (r <= 0)
                {
                    if (fr.Count > 0)
                    {
                        int r2 = fr[0].Item3;
                        regions[i] = r2;
                        for (int j = 1; j < fr.Count; j++)
                        {
                            if (fr[j].Item3 == r2)
                            {
                                continue;
                            }
                            ChangeRegion(fr[j].Item1, fr[j].Item2, s, fr[j].Item3, r2, regions);
                        }
                    }
                    else
                    {
                        regions[i] = ri;
                        ri++;
                    }
                }
            }
        }

        private void ChangeRegion(int x, int y, int s, int from, int to, NativeArray<int> regions)
        {
            int r = regions[x + y * s];
            if (r <= 0) { return; }
            regions[x + y * s] = to;
            for (int i = 0; i < eightDirections.Length; i++)
            {
                int x1 = x + eightDirections[i].x;
                int y1 = y + eightDirections[i].y;
                if (regions[x1 + y1 * s] == from)
                {
                    ChangeRegion(x1, y1, s, from, to, regions);
                }
            }
        }
        
        private void MakeSureThereIsTwoTilesSpaceBetweenSpots(NativeArray<int> occupied, NativeArray<int> regions, int s, int searchRange, int fromIndex, int toIndex)
        {
            NativeArray<int2> sideTiles = new NativeArray<int2>(s * s, Allocator.Temp);
            for (int i = fromIndex; i <= toIndex; i++)
            {
                if (occupied[i] < 0) { continue; }
                int x = i % s;
                int y = i / s;
                if (x < (offset - searchRange) || y < (offset - searchRange) || x >= (s - offset + searchRange) || y >= (s - offset + searchRange)) { continue; }
                bool clearMainTile = false;
                int sideTileCount = 0;
                for (int j = -1; j <= 1; j++)
                {
                    if (clearMainTile) { break; }
                    for (int k = -1; k <= 1; k++)
                    {
                        if (clearMainTile) { break; }
                        if (j == 0 && k == 0) { continue; }
                        int x1 = x + j;
                        int y1 = y + k;
                        int n1 = occupied[x1 + y1 * s];
                        if (n1 >= 0) { continue; }
                        int n = occupied[x + y * s];
                        for (int l = -1; l <= 1; l++)
                        {
                            if (clearMainTile) { break; }
                            for (int m = -1; m <= 1; m++)
                            {
                                if (clearMainTile) { break; }
                                if (l == 0 && m == 0) { continue; }
                                int x2 = x1 + l;
                                int y2 = y1 + m;
                                if (x == x2 && y == y2) { continue; }
                                int n2 = occupied[x2 + y2 * s];
                                if (n2 < 0) { continue; }
                                int2 t1 = new int2(x, y);
                                int2 t2 = new int2(x2, y2);
                                if (math.distance(t1, t2) <= 1f)
                                {
                                    continue;
                                }

                                if (n > n2)
                                {
                                    clearMainTile = true;
                                    occupied[x + y * s] = -1;
                                }
                                else if (n < n1)
                                {
                                    sideTiles[sideTileCount++] = new int2(x2, y2);
                                }
                                else
                                {
                                    // Check if the two tiles are connected and in the same region
                                    if (regions[i] == regions[x2 + y2 * s]) { continue; }
                                    int2 t = PositionTieBreaker(new int2(x, y), new int2(x2, y2));
                                    if (t.x == x && t.y == y)
                                    {
                                        clearMainTile = true;
                                        occupied[x + y * s] = -1;
                                    }
                                    else
                                    {
                                        sideTiles[sideTileCount++] = new int2(x2, y2);
                                    }
                                }
                            }
                        }
                    }
                }
                if (!clearMainTile)
                {
                    for (int j = 0; j < sideTileCount; j++)
                    {
                        int2 tile = sideTiles[j];
                        occupied[tile.x + tile.y * s] = -1;
                    }
                }
            }
            sideTiles.Dispose();
        }
        
        private void FloodFill(int x, int y, int s, int targetValue, NativeArray<int> occupiedHeights, NativeArray<bool> region, NativeArray<bool> processed, int chain = 0)
        {
            // Check if the current tile is out of bounds or doesn't match the target value
            if (x < 0 || x >= s || y < 0 || y >= s || chain >= s) { return; }
            if (region[x + y * s] || occupiedHeights[x + y * s] != targetValue) { return; }
            
            // Mark the current tile as part of the region
            region[x + y * s] = true;
            processed[x + y * s] = true;
            chain++;
            
            // Recursively flood-fill in all 8 directions
            for (int i = 0; i < eightDirections.Length; i++)
            {
                var x1 = x + eightDirections[i].x;
                var y1 = y + eightDirections[i].y;
                if (processed[x1 + y1 * s]) { continue; }
                FloodFill(x1, y1, s, targetValue, occupiedHeights, region, processed, chain);
            }
            //FloodFill(x + 1, y, s, targetValue, occupiedHeights, region, processed); // Right
            //FloodFill(x - 1, y, s, targetValue, occupiedHeights, region, processed); // Left
            //FloodFill(x, y + 1, s, targetValue, occupiedHeights, region, processed); // Up
            //FloodFill(x, y - 1, s, targetValue, occupiedHeights, region, processed); // Down
            //FloodFill(x + 1, y + 1, s, targetValue, occupiedHeights, region, processed); // Up-Right
            //FloodFill(x - 1, y - 1, s, targetValue, occupiedHeights, region, processed); // Down-Left
            //FloodFill(x + 1, y - 1, s, targetValue, occupiedHeights, region, processed); // Down-Right
            //FloodFill(x - 1, y + 1, s, targetValue, occupiedHeights, region, processed); // Up-Left
        }
        
        private void SetDistances()
        {
            int count = tilemaps[0].toPlace.Count + tilemaps[1].toPlace.Count + tilemaps[2].toPlace.Count + tilemaps[3].toPlace.Count;
            NativeArray<int2> cells = new NativeArray<int2>(count, Allocator.TempJob);
            NativeArray<int> maps = new NativeArray<int>(count, Allocator.Temp);
            int i = 0;
            foreach (var cell in tilemaps[0].toPlace)
            {
                cells[i] = new int2(cell.Key.x, cell.Key.y); maps[i] = 0; i++;
            }
            foreach (var cell in tilemaps[1].toPlace)
            {
                cells[i] = new int2(cell.Key.x, cell.Key.y); maps[i] = 1; i++;
            }
            foreach (var cell in tilemaps[2].toPlace)
            {
                cells[i] = new int2(cell.Key.x, cell.Key.y); maps[i] = 2; i++;
            }
            foreach (var cell in tilemaps[3].toPlace)
            {
                cells[i] = new int2(cell.Key.x, cell.Key.y); maps[i] = 3; i++;
            }
            NativeArray<float> distances = new NativeArray<float>(count, Allocator.TempJob);
            lastCell = tilemaps[0].tilemap.WorldToCell(infiniteTarget.position);
            var job = new CalculateDistancesJob
            {
                cells = cells,
                lastCell = new int2(lastCell.x, lastCell.y),
                distances = distances
            };
            JobHandle handle = job.Schedule(count, 64);
            handle.Complete();
            for (int j = 0; j < distances.Length; j++)
            {
                tilemaps[maps[j]].toPlace[new Vector3Int(cells[j].x, cells[j].y, 0)].distance = distances[j];
            }
            cells.Dispose();
            maps.Dispose();
            distances.Dispose();
        }
        
        public struct CalculateDistancesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int2> cells;
            [ReadOnly] public int2 lastCell;
            [WriteOnly] public NativeArray<float> distances;
            public void Execute(int index)
            {
                int2 cell = cells[index];
                float dx = cell.x - lastCell.x;
                float dy = cell.y - lastCell.y;
                distances[index] = math.sqrt(dx * dx + dy * dy);
            }
        }
        
        private void Add(Vector3Int cell, int tilemap, int tileset, int rule, GenerateType speed, int2 distanceCell)
        {
            tilemaps[tilemap].toClear.Remove(cell);
            CellData data = null;
            if (tilemaps[tilemap].placed.TryGetValue(cell, out data))
            {
                if (data.tileset == tileset && data.rule == rule)
                {
                    return;
                }
            }
            else if (tilemaps[tilemap].toPlace.TryGetValue(cell, out data))
            {
                if (data.tileset == tileset && data.rule == rule)
                {
                    return;
                }
            }
            else
            {
                data = new CellData();
            }
            data.tilemap = tilemap;
            data.rule = rule;
            data.tileset = tileset;
            float dx = cell.x - distanceCell.x;
            float dy = cell.y - distanceCell.y;
            data.distance = math.sqrt(dx * dx + dy * dy);
            if (speed == GenerateType.Immediate)
            {
                Place(cell, data);
            }
            else
            {
                tilemaps[tilemap].toPlace[cell] = data;
            }
        }

        private void Place(Vector3Int cell, CellData data)
        {
            tilemaps[data.tilemap].toPlace.Remove(cell);
            if (data.tilemap == 0 || data.tilemap == 2)
            {
                Sprite sprite = baseTile;
                if (data.tileset >= 0 && data.tilemap == 0)
                {
                    sprite = data.rule >= 0 ? tilesets[data.tileset].ruleTile.m_TilingRules[data.rule].m_Sprites[0] : tilesets[data.tileset].ruleTile.m_DefaultSprite;
                }
                else if (data.tileset >= 0 && data.tilemap == 2)
                {
                    if (data.rule == -6)
                    {
                        sprite = tilesets[data.tileset].heights.bottomSlope;
                    }
                    else if (data.rule == -7)
                    {
                        sprite = tilesets[data.tileset].heights.topSlope;
                    }
                    else if (data.rule == -8)
                    {
                        sprite = tilesets[data.tileset].heights.rightSlope;
                    }
                    else if (data.rule == -9)
                    {
                        sprite = tilesets[data.tileset].heights.leftSlope;
                    }
                    else
                    {
                        sprite = data.rule >= 0 ? tilesets[data.tileset].heights.ruleTile.m_TilingRules[data.rule].m_Sprites[0] : tilesets[data.tileset].heights.ruleTile.m_DefaultSprite;
                    }
                }
                else if (data.tileset == -1 && data.tilemap == 2)
                {
                    if (data.rule == -6)
                    {
                        sprite = baseHeights.bottomSlope;
                    }
                    else if (data.rule == -7)
                    {
                        sprite = baseHeights.topSlope;
                    }
                    else if (data.rule == -8)
                    {
                        sprite = baseHeights.rightSlope;
                    }
                    else if (data.rule == -9)
                    {
                        sprite = baseHeights.leftSlope;
                    }
                    else
                    {
                        sprite = data.rule >= 0 ? baseHeights.ruleTile.m_TilingRules[data.rule].m_Sprites[0] : baseHeights.ruleTile.m_DefaultSprite;
                    }
                }
                if (sprite != null)
                {
                    tilemaps[data.tilemap].tilemap.SetTile(cell, CreateTile(sprite, Color.white));
                    tilemaps[data.tilemap].placed[cell] = data;
                    data.collider = CreateCollider(cell, data.tilemap, data.tileset, data.rule);
                }
            }
            else
            {
                Sprite sprite = null;
                TilemapObject2D prefab = null;
                TilemapObjectData obj = null;
                if (data.tileset >= 0)
                {
                    obj = tilesets[data.tileset].objects[data.rule];
                }
                else
                {
                    obj = baseObjects[data.rule];
                }
                if (obj != null)
                {
                    if (obj.type == ObjectType.Prefab)
                    {
                        if (obj.prefabs != null)
                        {
                            int i = GetConsistentRandomIndex(new int2(cell.x, cell.y), obj.prefabs.Length);
                            if (i >= 0 && i < obj.prefabs.Length && obj.prefabs[i] != null)
                            {
                                prefab = obj.prefabs[i];
                            }
                        }
                    }
                    else
                    {
                        if (obj.sprites != null)
                        {
                            int i = GetConsistentRandomIndex(new int2(cell.x, cell.y), obj.sprites.Length);
                            if (i >= 0 && i < obj.sprites.Length && obj.sprites[i] != null)
                            {
                                sprite = obj.sprites[i];
                            }
                        }
                    }
                }
                if (prefab != null)
                {
                    data.Object2DReference = Instantiate(prefab, tilemaps[data.tilemap].tilemap.transform);
                    data.Object2DReference.transform.position = tilemaps[data.tilemap].tilemap.GetCellCenterWorld(cell);
                    tilemaps[data.tilemap].placed[cell] = data;
                    var renderers = data.Object2DReference.gameObject.GetComponentsInChildren<SpriteRenderer>();
                    if (renderers != null)
                    {
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            renderers[i].sortingOrder = tilemaps[data.tilemap].renderer.sortingOrder;
                        }
                    }
                }
                if (sprite != null)
                {
                    tilemaps[data.tilemap].tilemap.SetTile(cell, CreateTile(sprite, Color.white));
                    switch (obj.colliderType)
                    {
                        case Collider2DType.Circle:
                            CircleCollider2D circle = new GameObject("Collider_" + cell.ToString()).AddComponent<CircleCollider2D>();
                            circle.transform.SetParent(tilemaps[data.tilemap].tilemap.transform, true);
                            circle.transform.position = tilemaps[data.tilemap].tilemap.GetCellCenterWorld(cell);
                            circle.radius = obj.colliderSize;
                            data.collider = circle;
                            break;
                        case Collider2DType.Box:
                            BoxCollider2D box = new GameObject("Collider_" + cell.ToString()).AddComponent<BoxCollider2D>();
                            box.transform.SetParent(tilemaps[data.tilemap].tilemap.transform, true);
                            box.transform.position = tilemaps[data.tilemap].tilemap.GetCellCenterWorld(cell);
                            box.size = Vector2.one * obj.colliderSize;
                            data.collider = box;
                            break;
                    }
                    tilemaps[data.tilemap].placed[cell] = data;
                }
            }
        }
        
        private static int2 PositionTieBreaker(int2 a, int2 b)
        {
            if (a.x != b.x) { return a.x < b.x ? a : b; }
            return a.y < b.y ? a : b;
        }

        // Frequency is between 0 and 10
        private static bool ShouldPlaceSlope(int x, int y, int frequency)
        {
            if (frequency <= 0) { return false; }
            if (frequency > 10) { frequency = 10; }
            if (IsEven(x) != IsEven(y)) { return false; }
            int xi = MapNumberBetween0And9(x);
            int yi = MapNumberBetween0And9(y);
            NativeArray<int> digits = new NativeArray<int>(10, Allocator.Temp);
            digits[0] = 0; digits[1] = 4; digits[2] = 7; digits[3] = 2; digits[4] = 9; digits[5] = 3; digits[6] = 6; digits[7] = 1; digits[8] = 5; digits[9] = 8;
            for (int i = 0; i < frequency; i++)
            {
                if (digits[i] == xi || digits[i] == yi)
                {
                    digits.Dispose();
                    return true;
                }
            }
            digits.Dispose();
            return false;
        }
        
        private static int GetLastDigit(int x)
        {
            return math.abs(x) % 10; // Ensures the result is between 0 and 9
        }
        
        private static bool IsEven(int number)
        {
            return math.abs(number) % 2 == 0;
        }

        private static bool IsOdd(int number)
        {
            return math.abs(number) % 2 != 0;
        }
        
        private static int MapNumberBetween0And9(int number, int block = 20)
        {
            // Determine the position within the current block of 20
            int positionInBlock = math.abs(number) % block;

            // Map the position to a number between 0 and 9
            return positionInBlock / 2;
        }
        
        private static int GetConsistentRandomIndex(int2 cell, int count, uint seed = 0)
        {
            // Combine the coordinates and seed into a single hash
            uint hash = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663) ^ seed;

            // Map the hash value to the range [0, count-1]
            return (int)(hash % (uint)count);
        }
        
        private TileBase CreateTile(Sprite sprite, Color color)
        {
            if (sprite == null)
            {
                return null;
            }

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            // Calculate the scaling factor based on the sprite's size and PPU
            float spriteWidthInPixels = sprite.rect.width; // Width of the sprite in pixels
            float spriteHeightInPixels = sprite.rect.height; // Height of the sprite in pixels
            float spritePPU = sprite.pixelsPerUnit; // Pixels per unit of the sprite

            // Calculate the size of the sprite in units
            float spriteWidthInUnits = spriteWidthInPixels / spritePPU;
            float spriteHeightInUnits = spriteHeightInPixels / spritePPU;

            // Calculate the scaling factor to fit the sprite into a 1x1 grid
            float scaleX = 1f / spriteWidthInUnits;
            float scaleY = 1f / spriteHeightInUnits;

            // Apply the scaling factor to the tile
            tile.transform = Matrix4x4.Scale(new Vector3(scaleX, scaleY, 1f));
            return tile;
        }
        
        private static readonly (int2, bool)[] colliders_0_Top = new (int2, bool)[]
        {
            (new int2(0, 1), true),    // Up
            (new int2(0, -1), false),  // Down
            (new int2(-1, 1), true),   // Up-Left
            (new int2(1, 1), true),    // Up-Right
            (new int2(-1, -1), false), // Down-Left
            (new int2(1, -1), false)   // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_1_Down = new (int2, bool)[]
        {
            (new int2(0, 1), false),   // Up
            (new int2(0, -1), true),   // Down
            (new int2(-1, 1), false),  // Up-Left
            (new int2(1, 1), false),   // Up-Right
            (new int2(-1, -1), true),  // Down-Left
            (new int2(1, -1), true)    // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_2_Right = new (int2, bool)[]
        {
            (new int2(-1, 0), false),  // Left
            (new int2(1, 0), true),    // Right
            (new int2(-1, 1), false),  // Up-Left
            (new int2(1, 1), true),    // Up-Right
            (new int2(-1, -1), false), // Down-Left
            (new int2(1, -1), true)    // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_3_Left = new (int2, bool)[]
        {
            (new int2(-1, 0), true),   // Left
            (new int2(1, 0), false),   // Right
            (new int2(-1, 1), true),   // Up-Left
            (new int2(1, 1), false),   // Up-Right
            (new int2(-1, -1), true),  // Down-Left
            (new int2(1, -1), false)   // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_4_TopRight = new (int2, bool)[]
        {
            (new int2(0, -1), false),  // Down
            (new int2(-1, 0), false),  // Left
            (new int2(-1, 1), false),  // Up-Left
            (new int2(1, 1), true),    // Up-Right
            (new int2(-1, -1), false), // Down-Left
            (new int2(1, -1), false)   // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_5_TopLeft = new (int2, bool)[]
        {
            (new int2(0, -1), false),  // Down
            (new int2(1, 0), false),   // Right
            (new int2(-1, 1), true),   // Up-Left
            (new int2(1, 1), false),   // Up-Right
            (new int2(-1, -1), false), // Down-Left
            (new int2(1, -1), false)   // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_6_BottomRight = new (int2, bool)[]
        {
            (new int2(0, 1), false),   // Up
            (new int2(-1, 0), false),  // Left
            (new int2(-1, 1), false),  // Up-Left
            (new int2(1, 1), false),   // Up-Right
            (new int2(-1, -1), false), // Down-Left
            (new int2(1, -1), true)    // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_7_BottomLeft = new (int2, bool)[]
        {
            (new int2(0, 1), false),   // Up
            (new int2(1, 0), false),   // Right
            (new int2(-1, 1), false),  // Up-Left
            (new int2(1, 1), false),   // Up-Right
            (new int2(-1, -1), true),  // Down-Left
            (new int2(1, -1), false)   // Down-Right
        };
        
        private static readonly (int2, bool)[] colliders_8_TopRightArea = new (int2, bool)[]
        {
            (new int2(0, 1), true),    // Top
            (new int2(1, 0), true),    // Right
            (new int2(-1, 1), true),   // Top-Left
            (new int2(1, 1), true),    // Top-Right
            (new int2(-1, -1), false), // Bottom-Left
            (new int2(1, -1), true)    // Bottom-Right
        };
        
        private static readonly (int2, bool)[] colliders_9_TopLeftArea = new (int2, bool)[]
        {
            (new int2(0, 1), true),    // Top
            (new int2(-1, 0), true),   // Left
            (new int2(-1, 1), true),   // Top-Left
            (new int2(1, 1), true),    // Top-Right
            (new int2(-1, -1), true),  // Bottom-Left
            (new int2(1, -1), false)   // Bottom-Right
        };
        
        private static readonly (int2, bool)[] colliders_10_BottomRightArea = new (int2, bool)[]
        {
            (new int2(0, -1), true),   // Bottom
            (new int2(1, 0), true),    // Right
            (new int2(-1, 1), false),  // Top-Left
            (new int2(1, 1), true),    // Top-Right
            (new int2(-1, -1), true),  // Bottom-Left
            (new int2(1, -1), true)    // Bottom-Right
        };
        
        private static readonly (int2, bool)[] colliders_11_BottomLeftArea = new (int2, bool)[]
        {
            (new int2(0, -1), true),   // Bottom
            (new int2(-1, 0), true),   // Left
            (new int2(-1, 1), true),   // Top-Left
            (new int2(1, 1), false),   // Top-Right
            (new int2(-1, -1), true),  // Bottom-Left
            (new int2(1, -1), true)    // Bottom-Right
        };
        
        private Collider2D CreateCollider(Vector3Int cell, int tilemap, int tileset, int rule)
        {
            if (rule >= 0)
            {
                RuleTile.TilingRule tile = null;
                bool cliff = false;
                float cliffHorizontalPadding = 0;
                float thickness = 0.1f;
                if (tilemap == 0 && tileset >= 0)
                {
                    if (baseCollider != tilesets[tileset].collider)
                    {
                        tile = tilesets[tileset].ruleTile.m_TilingRules[rule];
                        thickness = tilesets[tileset].colliderThickness;
                    }
                }
                else if (tilemap == 2)
                {
                    cliff = true;
                    if (tileset >= 0)
                    {
                        tile = tilesets[tileset].heights.ruleTile.m_TilingRules[rule];
                        cliffHorizontalPadding = tilesets[tileset].heights.colliderHorizontalPadding;
                        thickness = tilesets[tileset].heights.colliderThickness;
                    }
                    else if (tileset >= -1)
                    {
                        tile = baseHeights.ruleTile.m_TilingRules[rule];
                        cliffHorizontalPadding = baseHeights.colliderHorizontalPadding;
                        thickness = baseHeights.colliderThickness;
                    }
                }
                if (tile != null)
                {
                    int best = -1;
                    int bestScore = -999;
                    
                    int score = GetRuleTileScore(tile, colliders_0_Top);
                    if (score > bestScore) { best = 0; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_1_Down);
                    if (score > bestScore) { best = 1; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_2_Right);
                    if (score > bestScore) { best = 2; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_3_Left);
                    if (score > bestScore) { best = 3; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_4_TopRight);
                    if (score > bestScore) { best = 4; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_5_TopLeft);
                    if (score > bestScore) { best = 5; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_6_BottomRight);
                    if (score > bestScore) { best = 6; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_7_BottomLeft);
                    if (score > bestScore) { best = 7; bestScore = score; }

                    score = GetRuleTileScore(tile, colliders_8_TopRightArea);
                    if (score > bestScore) { best = 8; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_9_TopLeftArea);
                    if (score > bestScore) { best = 9; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_10_BottomRightArea);
                    if (score > bestScore) { best = 10; bestScore = score; }
                    
                    score = GetRuleTileScore(tile, colliders_11_BottomLeftArea);
                    if (score > bestScore) { best = 11; bestScore = score; }
                    
                    var cellSize = tilemaps[tilemap].tilemap.cellSize;
                    if (best <= 1)
                    {
                        BoxCollider2D collider = new GameObject("Collider_" + cell.ToString()).AddComponent<BoxCollider2D>();
                        collider.transform.SetParent(tilemaps[tilemap].tilemap.transform, true);
                        collider.transform.position = tilemaps[tilemap].tilemap.GetCellCenterWorld(cell);
                        if (cliff && best == 0)
                        {
                            float s = cellSize.y * thickness * 0.5f;
                            collider.size = new Vector2(cellSize.x, cellSize.y * 0.5f + s);
                            collider.transform.position += new Vector3(0, s - collider.size.y * 0.5f, 0);
                        }
                        else
                        {
                            collider.size = new Vector2(cellSize.x, cellSize.y * thickness);
                        }
                        return collider;
                    }
                    else if (best <= 3)
                    {
                        BoxCollider2D collider = new GameObject("Collider_" + cell.ToString()).AddComponent<BoxCollider2D>();
                        collider.transform.SetParent(tilemaps[tilemap].tilemap.transform, true);
                        collider.transform.position = tilemaps[tilemap].tilemap.GetCellCenterWorld(cell);
                        collider.size = new Vector2(cellSize.x * thickness, cellSize.y);
                        return collider;
                    }
                    else if (best == 4 || best == 11)
                    {
                        PolygonCollider2D collider = new GameObject("Collider_" + cell.ToString()).AddComponent<PolygonCollider2D>();
                        collider.transform.SetParent(tilemaps[tilemap].tilemap.transform, true);
                        collider.transform.position = tilemaps[tilemap].tilemap.GetCellCenterWorld(cell);
                        if (cliff && best == 4)
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * cliffHorizontalPadding - cellSize.x * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * cliffHorizontalPadding - cellSize.x * 0.5f, cellSize.y * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, cellSize.y * 0.5f)
                            };
                        }
                        else
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * 0.5f, -cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, cellSize.y * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, cellSize.y * 0.5f)
                            };
                        }
                        return collider;
                    }
                    else if (best == 5 || best == 10)
                    {
                        PolygonCollider2D collider = new GameObject("Collider_" + cell.ToString()).AddComponent<PolygonCollider2D>();
                        collider.transform.SetParent(tilemaps[tilemap].tilemap.transform, true);
                        collider.transform.position = tilemaps[tilemap].tilemap.GetCellCenterWorld(cell);
                        if (cliff && best == 5)
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(-cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(-cellSize.x * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x  * 0.5f - cellSize.x * cliffHorizontalPadding, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * 0.5f - cellSize.x * cliffHorizontalPadding, cellSize.y * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, cellSize.y * 0.5f)
                            };
                        }
                        else
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(-cellSize.x * 0.5f, -cellSize.y * thickness * 0.5f),
                                new Vector2(-cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, cellSize.y * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, cellSize.y * 0.5f)
                            };
                        }
                        return collider;
                    }
                    else if (best == 6 || best == 9)
                    {
                        PolygonCollider2D collider = new GameObject("Collider_" + cell.ToString()).AddComponent<PolygonCollider2D>();
                        collider.transform.SetParent(tilemaps[tilemap].tilemap.transform, true);
                        collider.transform.position = tilemaps[tilemap].tilemap.GetCellCenterWorld(cell);
                        if (cliff && best == 9)
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, -cellSize.y * 0.5f)
                            };
                        }
                        else
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * 0.5f, -cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, -cellSize.y * 0.5f)
                            };
                        }
                        return collider;
                    }
                    else if (best == 7 || best == 8)
                    {
                        PolygonCollider2D collider = new GameObject("Collider_" + cell.ToString()).AddComponent<PolygonCollider2D>();
                        collider.transform.SetParent(tilemaps[tilemap].tilemap.transform, true);
                        collider.transform.position = tilemaps[tilemap].tilemap.GetCellCenterWorld(cell);
                        if (cliff && best == 8)
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(-cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(-cellSize.x * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, -cellSize.y * 0.5f)
                            };
                        }
                        else
                        {
                            collider.points = new Vector2[]
                            {
                                new Vector2(-cellSize.x * 0.5f, cellSize.y * thickness * 0.5f),
                                new Vector2(-cellSize.x * 0.5f, -cellSize.y * thickness * 0.5f),
                                new Vector2(cellSize.x * -thickness * 0.5f, -cellSize.y * 0.5f),
                                new Vector2(cellSize.x * thickness * 0.5f, -cellSize.y * 0.5f)
                            };
                        }
                        return collider;
                    }
                }
            }
            return null;
        }

        private int GetRuleTileScore(RuleTile.TilingRule tile, (int2, bool)[] factor)
        {
            int score = 0;
            for (int i = 0; i < tile.m_Neighbors.Count; i++)
            {
                var a = tile.m_NeighborPositions[i];
                for (int j = 0; j < factor.Length; j++)
                {
                    var b = factor[j];
                    if (a.x == b.Item1.x && a.y == b.Item1.y)
                    {
                        if ((b.Item2 && tile.m_Neighbors[i] == RuleTile.TilingRule.Neighbor.This) || (!b.Item2 && tile.m_Neighbors[i] == RuleTile.TilingRule.Neighbor.NotThis))
                        {
                            score++;
                        }
                        break;
                    }
                }
            }
            return score;
        }
        
        private void SetNoiseData()
        {
            if (tilesets != null)
            {
                for (int i = 0; i < tilesets.Count; i++)
                {
                    if (tilesets[i] != null)
                    {
                        if (tilesets[i].noise == null) { tilesets[i].noise = new NoiseGenerator2D(); }
                        tilesets[i].noise.SetNoiseType(tilesets[i].noiseType);
                        tilesets[i].noise.SetSeed(tilesets[i].noiseSeed);
                        tilesets[i].noise.SetFrequency(tilesets[i].noiseScale);
                        if (tilesets[i].heights != null)
                        {
                            if (tilesets[i].heights.noise == null) { tilesets[i].heights.noise = new NoiseGenerator2D(); }
                            tilesets[i].heights.noise.SetNoiseType(tilesets[i].heights.noiseType);
                            tilesets[i].heights.noise.SetSeed(tilesets[i].heights.noiseSeed);
                            tilesets[i].heights.noise.SetFrequency(tilesets[i].heights.noiseScale);
                        }
                        if (tilesets[i].objects != null)
                        {
                            for (int j = 0; j < tilesets[i].objects.Count; j++)
                            {
                                if (tilesets[i].objects[j].noise == null) { tilesets[i].objects[j].noise = new NoiseGenerator2D(); }
                                tilesets[i].objects[j].noise.SetNoiseType(tilesets[i].objects[j].noiseType);
                                tilesets[i].objects[j].noise.SetSeed(tilesets[i].objects[j].noiseSeed);
                                tilesets[i].objects[j].noise.SetFrequency(tilesets[i].objects[j].noiseScale);
                            }
                        }
                    }
                }
            }
            if (baseHeights != null)
            {
                if (baseHeights.noise == null) { baseHeights.noise = new NoiseGenerator2D(); }
                baseHeights.noise.SetNoiseType(baseHeights.noiseType);
                baseHeights.noise.SetSeed(baseHeights.noiseSeed);
                baseHeights.noise.SetFrequency(baseHeights.noiseScale);
            }
            if (baseObjects != null)
            {
                for (int j = 0; j < baseObjects.Count; j++)
                {
                    if (baseObjects[j].noise == null) { baseObjects[j].noise = new NoiseGenerator2D(); }
                    baseObjects[j].noise.SetNoiseType(baseObjects[j].noiseType);
                    baseObjects[j].noise.SetSeed(baseObjects[j].noiseSeed);
                    baseObjects[j].noise.SetFrequency(baseObjects[j].noiseScale);
                }
            }
        }
        
        private void CreateTilemapsIfNotExists()
        {
            if (grid == null)
            {
                grid = GetComponentInChildren<Grid>();
                if (grid == null)
                {
                    grid = new GameObject("Grid").AddComponent<Grid>();
                    grid.transform.SetParent(transform);
                    grid.transform.localPosition = Vector3.zero;
                }
                /* Causing issue
                SortingGroup group = GetComponent<SortingGroup>();
                if (group == null)
                {
                    group = gameObject.AddComponent<SortingGroup>();
                }
                */
            }
            if (tilemaps == null)
            {
                tilemaps = new List<TilemapData>();
            }
            if (tilemaps.Count != 4 || tilemaps[0] == null || tilemaps[1] == null || tilemaps[2] == null || tilemaps[3] == null)
            {
                Tilemap[] maps = GetComponentsInChildren<Tilemap>();
                if (maps != null && maps.Length != 4)
                {
                    for (int i = 0; i < maps.Length; i++)
                    {
                        #if UNITY_EDITOR
                        if (Application.isPlaying)
                        {
                            Destroy(maps[i].gameObject);
                        }
                        else
                        {
                            DestroyImmediate(maps[i].gameObject);
                        }
                        #else
                        Destroy(maps[i].gameObject);
                        #endif
                    }
                    maps = null;
                }
                if (maps == null)
                {
                    maps = new Tilemap[4];
                    Tilemap tilemap = new GameObject("Tilemap_0_Base").AddComponent<Tilemap>();
                    tilemap.transform.SetParent(grid.transform);
                    tilemap.transform.localPosition = Vector3.zero;
                    TilemapRenderer renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
                    renderer.sortingOrder = playerSortLayer - 2;
                    renderer.mode = TilemapRenderer.Mode.Chunk;
                    maps[0] = tilemap;
                    
                    tilemap = new GameObject("Tilemap_1_BaseObjects").AddComponent<Tilemap>();
                    tilemap.transform.SetParent(grid.transform);
                    tilemap.transform.localPosition = Vector3.zero;
                    renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
                    renderer.sortingOrder = playerSortLayer;
                    renderer.mode = TilemapRenderer.Mode.Individual;
                    maps[1] = tilemap;
                    
                    tilemap = new GameObject("Tilemap_2_Height").AddComponent<Tilemap>();
                    tilemap.transform.SetParent(grid.transform);
                    tilemap.transform.localPosition = Vector3.zero;
                    renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
                    renderer.sortingOrder = playerSortLayer - 1;
                    renderer.mode = TilemapRenderer.Mode.Chunk;
                    maps[2] = tilemap;
                    
                    tilemap = new GameObject("Tilemap_3_HeightObjects").AddComponent<Tilemap>();
                    tilemap.transform.SetParent(grid.transform);
                    tilemap.transform.localPosition = Vector3.zero;
                    renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
                    renderer.sortingOrder = playerSortLayer;
                    renderer.mode = TilemapRenderer.Mode.Individual;
                    maps[3] = tilemap;
                }
                tilemaps.Clear();
                for (int i = 0; i < maps.Length; i++)
                {
                    TilemapData data = new TilemapData();
                    data.tilemap = maps[i];
                    data.renderer = maps[i].GetComponent<TilemapRenderer>();
                    data.placed = new Dictionary<Vector3Int, CellData>();
                    data.toPlace = new Dictionary<Vector3Int, CellData>();
                    data.toClear = new HashSet<Vector3Int>();
                    tilemaps.Add(data);
                }
            }
        }
        
        private struct AddBaseTilesJob : IJobParallelFor
        {
            // Input data
            [ReadOnly] public int s;
            [ReadOnly] public int offset;
            [ReadOnly] public int2 bottomLeftCell;
            [ReadOnly] public NativeArray<int> occupied;
            [ReadOnly] public int anyTile;
            [ReadOnly] public NativeArray<RuleTileData> rules;
            
            public NativeArray<int> occupiedTransitions;
            public NativeArray<AddData> addedCells;
            
            public void Execute(int i)
            {
                int x = i % s;
                int y = i / s;
                if (x < offset || y < offset || x >= (s - offset) || y >= (s - offset)) { return; }
                AddData addData = new AddData(-1, -1, -1, 0, 0, 0);
                Vector3Int cell = new Vector3Int(bottomLeftCell.x + x - offset, bottomLeftCell.y + y - offset, 0);
                addData.cell = new int3(cell.x, cell.y, 0);
                int n = occupied[i];
                if (n >= 0)
                {
                    addData.tilemap = 0;
                    addData.tileset = n;
                    addData.rule = -1;
                }
                else
                {
                    NativeArray<bool> neighbors = new NativeArray<bool>(eightDirections.Length, Allocator.Temp);
                    int transition = -1;
                    int count = 0;
                    bool tp = false, lf = false, rt = false, dn = false;
                    for (int j = 0; j < eightDirections.Length; j++)
                    {
                        var dir = eightDirections[j];
                        int t = occupied[(x + dir.x) + (y + dir.y) * s];
                        if (t >= 0)
                        {
                            transition = t;
                            count++;
                            if (dir.x == 1 && dir.y == 0) { rt = true; }
                            else if (dir.x == -1 && dir.y == 0) { lf = true; }
                            else if (dir.x == 0 && dir.y == 1) { tp = true; }
                            else if (dir.x == 0 && dir.y == -1) { dn = true; }
                        }
                        neighbors[j] = t >= 0;
                    }
                    if (count > 0)
                    {
                        NativeArray<int4> neighborDict = new NativeArray<int4>(eightDirections.Length, Allocator.Temp);
                        occupiedTransitions[i] = transition;
                        if (rt && dn)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                int4 d = new int4(p.x, p.y, 0, 0);
                                if (p.x == -1 && p.y == 1) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(-1, 0)) || p.Equals(new int2(0, 1))) { d.w = anyTile; }
                                else { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                neighborDict[j] = d;
                            }
                        }
                        else if (lf && tp)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                int4 d = new int4(p.x, p.y, 0, 0);
                                if (p.x == 1 && p.y == -1) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(1, 0)) || p.Equals(new int2(0, -1))) { d.w = anyTile; }
                                else { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                neighborDict[j] = d;
                            }
                        }
                        else if (rt && tp)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                int4 d = new int4(p.x, p.y, 0, 0);
                                if (p.x == -1 && p.y == -1) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(-1, 0)) || p.Equals(new int2(0, -1))) { d.w = anyTile; }
                                else { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                neighborDict[j] = d;
                            }
                        }
                        else if (lf && dn)
                        {
                            for (int j = 0; j < eightDirections.Length; j++)
                            {
                                var p = eightDirections[j];
                                int4 d = new int4(p.x, p.y, 0, 0);
                                if (p.x == 1 && p.y == 1) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                else if (p.Equals(new int2(1, 0)) || p.Equals(new int2(0, 1))) { d.w = anyTile; }
                                else { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                neighborDict[j] = d;
                            }
                        }
                        else
                        {
                            if (count == 1 && !tp && !rt && !lf && !dn)
                            {
                                if (occupied[(x + 1) + (y + 1) * s] >= 0)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.x == 1 && p.y == 1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (!p.Equals(new int2(1, 0)) && !p.Equals(new int2(0, 1))) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        else { d.w = anyTile; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else if (occupied[(x - 1) + (y + 1) * s] >= 0)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.x == -1 && p.y == 1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (!p.Equals(new int2(-1, 0)) && !p.Equals(new int2(0, 1))) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        else { d.w = anyTile; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else if (occupied[(x + 1) + (y - 1) * s] >= 0)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.x == 1 && p.y == -1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (!p.Equals(new int2(1, 0)) && !p.Equals(new int2(0, -1))) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        else { d.w = anyTile; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else if (occupied[(x - 1) + (y - 1) * s] >= 0)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.x == -1 && p.y == -1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (!p.Equals(new int2(-1, 0)) && !p.Equals(new int2(0, -1))) { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        else { d.w = anyTile; }
                                        neighborDict[j] = d;
                                    }
                                }
                            }
                            else
                            {
                                if (tp)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.y == 1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (p.y == 0) { d.w = anyTile; }
                                        else { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else if (dn)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.y == -1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (p.y == 0) { d.w = anyTile; }
                                        else { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else if (rt)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.x == 1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (p.x == 0) { d.w = anyTile; }
                                        else { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else if (lf)
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        if (p.x == -1) { d.w = RuleTile.TilingRuleOutput.Neighbor.This; }
                                        else if (p.x == 0) { d.w = anyTile; }
                                        else { d.w = RuleTile.TilingRuleOutput.Neighbor.NotThis; }
                                        neighborDict[j] = d;
                                    }
                                }
                                else
                                {
                                    for (int j = 0; j < eightDirections.Length; j++)
                                    {
                                        var p = eightDirections[j];
                                        int4 d = new int4(p.x, p.y, 0, 0);
                                        d.w = neighbors[j] ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis;
                                        neighborDict[j] = d;
                                    }
                                }
                            }
                        }
                        
                        // Variables to track the best matching rule
                        int bestRuleIndex = -1;
                        int bestScore = -999;
                        bool bestValid = false;
                        int processedRules = 0;
                        
                        for (int k = 0; k < rules.Length; k++)
                        {
                            if (rules[k].tileset != transition) { continue; }
                            int score = 0;
                            bool valid = false;
                            for (int j = 0; j < rules.Length; j++)
                            {
                                var rule = rules[j];
                                if (rule.tileset != transition || rule.rule != processedRules) { continue; }
                                var p = rule.neighborPosition;
                                valid = rule.valid;
                                for (int l = 0; l < neighborDict.Length; l++)
                                {
                                    var nd = neighborDict[l];
                                    if (nd.x == p.x && nd.y == p.y)
                                    {
                                        var ne = rule.neighborValue;
                                        // If the neighbor state matches the rule, increase the score
                                        if (nd.w == ne) { score++; }
                                        else if (ne != anyTile) { score--; }
                                        break;
                                    }
                                }
                            }

                            // Update the best rule if this rule has a higher score
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestRuleIndex = processedRules;
                                bestValid = valid;
                            }

                            if (processedRules >= rules[k].maxRules) { break; }
                            processedRules++;
                        }

                        // Set the tile based on the best matching rule
                        if (bestValid)
                        {
                            // Use the first sprite from the best matching rule (or apply randomization if needed)
                            addData.tilemap = 0;
                            addData.tileset = transition;
                            addData.rule = bestRuleIndex;
                        }
                        else
                        {
                            // Fallback to the default sprite of the RuleTile
                            addData.tilemap = 0;
                            addData.tileset = transition;
                            addData.rule = -1;
                        }
                    }
                    else
                    {
                        addData.tilemap = 0;
                        addData.tileset = -1;
                        addData.rule = -1;
                    }
                }
                addedCells[i] = addData;
            }
        }
        
        #region Editor
        #if UNITY_EDITOR
        [HideInInspector] public int editorTabIndex = 0;
        public void EditorNoiseDataUpdated()
        {
            SetNoiseData();
        }
        
        public void EditorInstanceSelected()
        {
            CreateTilemapsIfNotExists();
        }
        
        public void EditorClear()
        {
            Clear(ClearType.ClearPreviousImmediate);
        }
        #endif
        #endregion
        
    }
}