using System;
using System.Collections.Generic;
using System.Linq;

namespace CommonLib
{
    /// <summary>
    /// 그래프의 엣지를 나타내는 구조체
    /// </summary>
    /// <typeparam name="TNode">노드 타입</typeparam>
    public struct Edge<TNode> where TNode : notnull
    {
        public TNode From { get; set; }
        public TNode To { get; set; }
        public double Weight { get; set; }

        public Edge(TNode from, TNode to, double weight = 1.0)
        {
            From = from;
            To = to;
            Weight = weight;
        }

        public override string ToString() => $"{From} -> {To} (Weight: {Weight})";
    }

    /// <summary>
    /// 경로 탐색 결과를 나타내는 클래스
    /// </summary>
    /// <typeparam name="TNode">노드 타입</typeparam>
    public class PathResult<TNode> where TNode : notnull
    {
        public List<TNode> Path { get; set; }
        public double TotalWeight { get; set; }
        public DateTime CachedAt { get; set; }

        public PathResult(List<TNode> path, double totalWeight)
        {
            Path = path;
            TotalWeight = totalWeight;
            CachedAt = DateTime.UtcNow;
        }

        public bool IsExpired(TimeSpan cacheExpiration)
        {
            return DateTime.UtcNow - CachedAt > cacheExpiration;
        }
    }

    /// <summary>
    /// MST (Minimum Spanning Tree) 결과를 나타내는 클래스
    /// </summary>
    /// <typeparam name="TNode">노드 타입</typeparam>
    public class MSTResult<TNode> where TNode : notnull
    {
        public List<Edge<TNode>> Edges { get; set; }
        public double TotalWeight { get; set; }

        public MSTResult(List<Edge<TNode>> edges, double totalWeight)
        {
            Edges = edges;
            TotalWeight = totalWeight;
        }
    }

    /// <summary>
    /// 우선순위 큐 (Min Heap)
    /// </summary>
    /// <typeparam name="T">요소 타입</typeparam>
    public class PriorityQueue<T>
    {
        private List<(T item, double priority)> _heap;

        public int Count => _heap.Count;

        public PriorityQueue()
        {
            _heap = new List<(T, double)>();
        }

        public void Enqueue(T item, double priority)
        {
            _heap.Add((item, priority));
            HeapifyUp(_heap.Count - 1);
        }

        public T Dequeue()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Queue is empty");

            var result = _heap[0].item;
            _heap[0] = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);

            if (_heap.Count > 0)
                HeapifyDown(0);

            return result;
        }

        public bool IsEmpty() => _heap.Count == 0;

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_heap[index].priority >= _heap[parentIndex].priority)
                    break;

                (_heap[index], _heap[parentIndex]) = (_heap[parentIndex], _heap[index]);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            while (true)
            {
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;
                int smallest = index;

                if (leftChild < _heap.Count && _heap[leftChild].priority < _heap[smallest].priority)
                    smallest = leftChild;

                if (rightChild < _heap.Count && _heap[rightChild].priority < _heap[smallest].priority)
                    smallest = rightChild;

                if (smallest == index)
                    break;

                (_heap[index], _heap[smallest]) = (_heap[smallest], _heap[index]);
                index = smallest;
            }
        }
    }

    /// <summary>
    /// Union-Find (Disjoint Set) 자료구조
    /// </summary>
    /// <typeparam name="T">요소 타입</typeparam>
    public class UnionFind<T> where T : notnull
    {
        private Dictionary<T, T> _parent;
        private Dictionary<T, int> _rank;

        public UnionFind()
        {
            _parent = new Dictionary<T, T>();
            _rank = new Dictionary<T, int>();
        }

        public void MakeSet(T item)
        {
            if (!_parent.ContainsKey(item))
            {
                _parent[item] = item;
                _rank[item] = 0;
            }
        }

        public T Find(T item)
        {
            if (!_parent.ContainsKey(item))
                MakeSet(item);

            if (!_parent[item].Equals(item))
                _parent[item] = Find(_parent[item]); // 경로 압축

            return _parent[item];
        }

        public bool Union(T x, T y)
        {
            T rootX = Find(x);
            T rootY = Find(y);

            if (rootX.Equals(rootY))
                return false; // 이미 같은 집합

            // Union by rank
            if (_rank[rootX] < _rank[rootY])
            {
                _parent[rootX] = rootY;
            }
            else if (_rank[rootX] > _rank[rootY])
            {
                _parent[rootY] = rootX;
            }
            else
            {
                _parent[rootY] = rootX;
                _rank[rootX]++;
            }

            return true;
        }
    }

    /// <summary>
    /// 고급 기능을 갖춘 범용 그래프 자료구조
    /// - MST (Minimum Spanning Tree): Kruskal, Prim
    /// - 최단 경로: Dijkstra
    /// - 경로 캐싱 시스템
    /// </summary>
    /// <typeparam name="TNode">노드 타입</typeparam>
    public class Graph<TNode> where TNode : notnull
    {
        private Dictionary<TNode, List<Edge<TNode>>> _adjacencyList;
        private Dictionary<string, PathResult<TNode>> _pathCache;
        private List<Edge<TNode>>? _cachedEdgeList;
        private TimeSpan _cacheExpiration;
        private bool _isDirected;

        public int NodeCount => _adjacencyList.Count;
        public int EdgeCount => _adjacencyList.Values.Sum(edges => edges.Count) / (_isDirected ? 1 : 2);

        /// <summary>
        /// 그래프 생성자
        /// </summary>
        /// <param name="isDirected">방향 그래프 여부</param>
        /// <param name="cacheExpirationMinutes">캐시 만료 시간 (분)</param>
        public Graph(bool isDirected = false, int cacheExpirationMinutes = 30)
        {
            _adjacencyList = new Dictionary<TNode, List<Edge<TNode>>>();
            _pathCache = new Dictionary<string, PathResult<TNode>>();
            _cachedEdgeList = null;
            _cacheExpiration = TimeSpan.FromMinutes(cacheExpirationMinutes);
            _isDirected = isDirected;
        }

        /// <summary>
        /// 노드 추가
        /// </summary>
        public void AddNode(TNode node)
        {
            if (!_adjacencyList.ContainsKey(node))
            {
                _adjacencyList[node] = new List<Edge<TNode>>();
            }
        }

        /// <summary>
        /// 엣지 추가
        /// </summary>
        public void AddEdge(TNode from, TNode to, double weight = 1.0)
        {
            AddNode(from);
            AddNode(to);

            _adjacencyList[from].Add(new Edge<TNode>(from, to, weight));

            if (!_isDirected)
            {
                _adjacencyList[to].Add(new Edge<TNode>(to, from, weight));
            }

            // 그래프 변경 시 캐시 무효화
            InvalidateAllCaches();
        }

        /// <summary>
        /// 엣지 제거
        /// </summary>
        public bool RemoveEdge(TNode from, TNode to)
        {
            if (!_adjacencyList.ContainsKey(from))
                return false;

            bool removed = _adjacencyList[from].RemoveAll(e => e.To.Equals(to)) > 0;

            if (!_isDirected && _adjacencyList.ContainsKey(to))
            {
                _adjacencyList[to].RemoveAll(e => e.To.Equals(from));
            }

            if (removed)
                InvalidateAllCaches();

            return removed;
        }

        /// <summary>
        /// 노드의 이웃 노드들 가져오기
        /// </summary>
        public IEnumerable<Edge<TNode>> GetNeighbors(TNode node)
        {
            if (_adjacencyList.ContainsKey(node))
                return _adjacencyList[node];
            return Enumerable.Empty<Edge<TNode>>();
        }

        /// <summary>
        /// 모든 엣지 가져오기 (캐싱됨)
        /// </summary>
        public IEnumerable<Edge<TNode>> GetAllEdges()
        {
            if (_cachedEdgeList != null)
                return _cachedEdgeList;

            var edges = new List<Edge<TNode>>();
            var seen = new HashSet<(TNode, TNode)>();

            foreach (var kvp in _adjacencyList)
            {
                foreach (var edge in kvp.Value)
                {
                    if (_isDirected)
                    {
                        edges.Add(edge);
                    }
                    else
                    {
                        var pair = (edge.From, edge.To);
                        var reversePair = (edge.To, edge.From);

                        if (!seen.Contains(pair) && !seen.Contains(reversePair))
                        {
                            edges.Add(edge);
                            seen.Add(pair);
                        }
                    }
                }
            }

            _cachedEdgeList = edges;
            return _cachedEdgeList;
        }

        /// <summary>
        /// Dijkstra 알고리즘을 이용한 최단 경로 탐색 (캐싱 지원)
        /// </summary>
        public PathResult<TNode>? FindShortestPath(TNode start, TNode end, bool useCache = true)
        {
            string cacheKey = $"{start}_{end}";

            // 캐시 확인
            if (useCache && _pathCache.TryGetValue(cacheKey, out var cachedResult))
            {
                if (!cachedResult.IsExpired(_cacheExpiration))
                {
                    return cachedResult;
                }
                else
                {
                    _pathCache.Remove(cacheKey);
                }
            }

            // Dijkstra 알고리즘 실행
            var distances = new Dictionary<TNode, double>();
            var previous = new Dictionary<TNode, TNode>();
            var priorityQueue = new PriorityQueue<TNode>();
            var visited = new HashSet<TNode>();

            foreach (var node in _adjacencyList.Keys)
            {
                distances[node] = double.MaxValue;
            }
            distances[start] = 0;

            priorityQueue.Enqueue(start, 0);

            while (!priorityQueue.IsEmpty())
            {
                var current = priorityQueue.Dequeue();

                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                if (current.Equals(end))
                    break;

                foreach (var edge in GetNeighbors(current))
                {
                    double newDist = distances[current] + edge.Weight;

                    if (newDist < distances[edge.To])
                    {
                        distances[edge.To] = newDist;
                        previous[edge.To] = current;
                        priorityQueue.Enqueue(edge.To, newDist);
                    }
                }
            }

            // 경로 재구성
            if (!previous.ContainsKey(end) && !start.Equals(end))
                return null; // 경로 없음

            var path = new List<TNode>();
            var currentNode = end;

            while (!currentNode.Equals(start))
            {
                path.Add(currentNode);
                if (!previous.ContainsKey(currentNode))
                    return null;
                currentNode = previous[currentNode];
            }
            path.Add(start);
            path.Reverse();

            var result = new PathResult<TNode>(path, distances[end]);

            // 캐시 저장
            if (useCache)
            {
                _pathCache[cacheKey] = result;
            }

            return result;
        }

        /// <summary>
        /// Kruskal 알고리즘을 이용한 최소 신장 트리 (MST) 생성
        /// </summary>
        public MSTResult<TNode>? KruskalMST()
        {
            if (_isDirected)
                throw new InvalidOperationException("MST는 무방향 그래프에서만 동작합니다.");

            var edges = GetAllEdges().OrderBy(e => e.Weight).ToList();
            var unionFind = new UnionFind<TNode>();
            var mstEdges = new List<Edge<TNode>>();
            double totalWeight = 0;

            foreach (var node in _adjacencyList.Keys)
            {
                unionFind.MakeSet(node);
            }

            foreach (var edge in edges)
            {
                if (unionFind.Union(edge.From, edge.To))
                {
                    mstEdges.Add(edge);
                    totalWeight += edge.Weight;

                    if (mstEdges.Count == NodeCount - 1)
                        break;
                }
            }

            if (mstEdges.Count != NodeCount - 1)
                return null; // 그래프가 연결되지 않음

            return new MSTResult<TNode>(mstEdges, totalWeight);
        }

        /// <summary>
        /// Prim 알고리즘을 이용한 최소 신장 트리 (MST) 생성
        /// </summary>
        public MSTResult<TNode>? PrimMST(TNode? startNode = default)
        {
            if (_isDirected)
                throw new InvalidOperationException("MST는 무방향 그래프에서만 동작합니다.");

            if (NodeCount == 0)
                return null;

            var start = startNode ?? _adjacencyList.Keys.First();
            var mstEdges = new List<Edge<TNode>>();
            var visited = new HashSet<TNode>();
            var priorityQueue = new PriorityQueue<Edge<TNode>>();
            double totalWeight = 0;

            visited.Add(start);

            foreach (var edge in GetNeighbors(start))
            {
                priorityQueue.Enqueue(edge, edge.Weight);
            }

            while (!priorityQueue.IsEmpty() && visited.Count < NodeCount)
            {
                var edge = priorityQueue.Dequeue();

                if (visited.Contains(edge.To))
                    continue;

                visited.Add(edge.To);
                mstEdges.Add(edge);
                totalWeight += edge.Weight;

                foreach (var nextEdge in GetNeighbors(edge.To))
                {
                    if (!visited.Contains(nextEdge.To))
                    {
                        priorityQueue.Enqueue(nextEdge, nextEdge.Weight);
                    }
                }
            }

            if (mstEdges.Count != NodeCount - 1)
                return null; // 그래프가 연결되지 않음

            return new MSTResult<TNode>(mstEdges, totalWeight);
        }

        /// <summary>
        /// 모든 노드 쌍에 대한 최단 경로 미리 계산 (캐시 워밍)
        /// </summary>
        public void PrecomputeAllPaths()
        {
            var nodes = _adjacencyList.Keys.ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    FindShortestPath(nodes[i], nodes[j], useCache: true);
                    if (_isDirected)
                    {
                        FindShortestPath(nodes[j], nodes[i], useCache: true);
                    }
                }
            }
        }

        /// <summary>
        /// 경로 캐시 무효화
        /// </summary>
        public void InvalidateCache()
        {
            _pathCache.Clear();
        }

        /// <summary>
        /// 모든 캐시 무효화 (경로 캐시 + 엣지 리스트 캐시)
        /// </summary>
        private void InvalidateAllCaches()
        {
            _pathCache.Clear();
            _cachedEdgeList = null;
        }

        /// <summary>
        /// 캐시 통계 정보
        /// </summary>
        public (int Count, int Expired) GetCacheStats()
        {
            int expiredCount = _pathCache.Values.Count(p => p.IsExpired(_cacheExpiration));
            return (_pathCache.Count, expiredCount);
        }

        /// <summary>
        /// 만료된 캐시 정리
        /// </summary>
        public void CleanExpiredCache()
        {
            var expiredKeys = _pathCache
                .Where(kvp => kvp.Value.IsExpired(_cacheExpiration))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _pathCache.Remove(key);
            }
        }

        /// <summary>
        /// 그래프를 문자열로 표현
        /// </summary>
        public override string ToString()
        {
            var lines = new List<string>
            {
                $"Graph ({(_isDirected ? "Directed" : "Undirected")})",
                $"Nodes: {NodeCount}, Edges: {EdgeCount}",
                $"Cache: {_pathCache.Count} entries",
                "Edges:"
            };

            foreach (var edge in GetAllEdges().Take(10))
            {
                lines.Add($"  {edge}");
            }

            if (GetAllEdges().Count() > 10)
            {
                lines.Add($"  ... and {GetAllEdges().Count() - 10} more");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
