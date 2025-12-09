# Graph 자료구조 사용 가이드

CommonLib의 범용 그래프 자료구조는 고급 알고리즘과 캐싱 시스템을 제공합니다.

## 주요 기능

### 1. 그래프 알고리즘
- **Dijkstra**: 최단 경로 탐색
- **Kruskal MST**: 최소 신장 트리 (간선 기반)
- **Prim MST**: 최소 신장 트리 (노드 기반)

### 2. 캐싱 시스템
- **경로 탐색 결과 자동 캐싱**: 최단 경로 탐색 결과 캐싱
- **엣지 리스트 캐싱**: `GetAllEdges()` 호출 결과 캐싱으로 반복 조회 성능 향상
- **만료 시간 설정 가능**: 캐시 유효 기간 설정
- **캐시 통계 및 관리 기능**: 캐시 통계 조회 및 정리

### 3. 지원 기능
- 방향/무방향 그래프
- 가중치 그래프
- 제네릭 타입 노드 지원

## 사용 예제

### 기본 그래프 생성

```csharp
// 무방향 그래프 생성 (캐시 만료: 30분)
var graph = new Graph<string>(isDirected: false, cacheExpirationMinutes: 30);

// 노드와 간선 추가
graph.AddEdge("서울", "대전", 150);
graph.AddEdge("대전", "대구", 120);
graph.AddEdge("대구", "부산", 90);
```

### Dijkstra 최단 경로

```csharp
var path = graph.FindShortestPath("서울", "부산");
if (path != null)
{
    Console.WriteLine($"경로: {string.Join(" -> ", path.Path)}");
    Console.WriteLine($"총 거리: {path.TotalWeight}");
}

// 캐시된 결과 재사용 (빠른 조회)
var cachedPath = graph.FindShortestPath("서울", "부산"); // 즉시 반환
```

### Kruskal MST (최소 신장 트리)

```csharp
var graph = new Graph<string>(isDirected: false);

graph.AddEdge("A", "B", 4);
graph.AddEdge("B", "C", 8);
graph.AddEdge("A", "C", 7);
graph.AddEdge("C", "D", 5);

var mst = graph.KruskalMST();
if (mst != null)
{
    Console.WriteLine($"총 비용: {mst.TotalWeight}");
    foreach (var edge in mst.Edges)
    {
        Console.WriteLine($"{edge.From} -> {edge.To}: {edge.Weight}");
    }
}
```

### Prim MST

```csharp
var graph = new Graph<int>(isDirected: false);

graph.AddEdge(0, 1, 2);
graph.AddEdge(1, 2, 3);
graph.AddEdge(0, 3, 6);

var mst = graph.PrimMST(startNode: 0);
Console.WriteLine($"총 비용: {mst.TotalWeight}");
```

### 방향 그래프

```csharp
var taskGraph = new Graph<string>(isDirected: true);

taskGraph.AddEdge("Task1", "Task2", 1);
taskGraph.AddEdge("Task2", "Task3", 2);
taskGraph.AddEdge("Task1", "Task3", 5);

// Task1에서 Task3로 가는 최단 경로 찾기
var path = taskGraph.FindShortestPath("Task1", "Task3");
```

### 캐시 관리

```csharp
// 모든 경로 사전 계산 (캐시 워밍)
graph.PrecomputeAllPaths();

// 캐시 통계 확인
var (count, expired) = graph.GetCacheStats();
Console.WriteLine($"캐시: {count}개 항목, {expired}개 만료");

// 만료된 캐시 정리
graph.CleanExpiredCache();

// 캐시 무효화 (그래프 변경 시 자동 호출됨)
graph.InvalidateCache();
```

### 그래프 정보 조회

```csharp
// 노드 개수
int nodeCount = graph.NodeCount;

// 간선 개수
int edgeCount = graph.EdgeCount;

// 이웃 노드 조회
var neighbors = graph.GetNeighbors("서울");
foreach (var edge in neighbors)
{
    Console.WriteLine($"{edge.To}: {edge.Weight}");
}

// 모든 간선 조회 (첫 호출 시 계산 후 캐싱됨)
var allEdges = graph.GetAllEdges();  // 계산 후 캐싱
var allEdges2 = graph.GetAllEdges(); // 캐시된 결과 반환 (빠름)
```

## 실전 활용 사례

### 1. 도시 간 최단 경로

```csharp
var cityGraph = new Graph<string>(isDirected: false);

cityGraph.AddEdge("서울", "대전", 150);
cityGraph.AddEdge("서울", "강릉", 165);
cityGraph.AddEdge("대전", "대구", 120);
cityGraph.AddEdge("대구", "부산", 90);
cityGraph.AddEdge("강릉", "부산", 280);

var route = cityGraph.FindShortestPath("서울", "부산");
// 결과: 서울 -> 대전 -> 대구 -> 부산 (360km)
```

### 2. 네트워크 라우팅

```csharp
var network = new Graph<string>(isDirected: false);

// 라우터 간 지연시간(ms)
network.AddEdge("Router1", "Router2", 10);
network.AddEdge("Router2", "Router3", 15);
network.AddEdge("Router3", "Router4", 5);

var path = network.FindShortestPath("Router1", "Router4");
Console.WriteLine($"최소 지연시간: {path.TotalWeight}ms");
```

### 3. 전력망 설계 (MST)

```csharp
var powerGrid = new Graph<string>(isDirected: false);

// 각 연결 비용
powerGrid.AddEdge("발전소", "변전소A", 100);
powerGrid.AddEdge("발전소", "변전소B", 150);
powerGrid.AddEdge("변전소A", "변전소C", 80);
powerGrid.AddEdge("변전소B", "변전소C", 120);

var mst = powerGrid.KruskalMST();
Console.WriteLine($"최소 건설 비용: {mst.TotalWeight}");
```

### 4. 작업 의존성 (방향 그래프)

```csharp
var workflow = new Graph<string>(isDirected: true);

workflow.AddEdge("설계", "개발", 1);
workflow.AddEdge("개발", "테스트", 1);
workflow.AddEdge("테스트", "배포", 1);
workflow.AddEdge("설계", "문서화", 1);
workflow.AddEdge("문서화", "배포", 1);

var criticalPath = workflow.FindShortestPath("설계", "배포");
```

## 성능 최적화

### 캐싱 전략

```csharp
// 자주 조회되는 경로는 사전 계산
graph.PrecomputeAllPaths();

// 캐시 사용/미사용 선택
var freshPath = graph.FindShortestPath(start, end, useCache: false);
var cachedPath = graph.FindShortestPath(start, end, useCache: true);

// 엣지 리스트도 자동 캐싱됨
var edges = graph.GetAllEdges();  // 첫 호출: 계산 후 캐싱
var edges2 = graph.GetAllEdges(); // 이후 호출: 즉시 반환 (매우 빠름)
```

### 대용량 그래프

```csharp
// 캐시 만료 시간을 짧게 설정하여 메모리 절약
var largeGraph = new Graph<int>(isDirected: false, cacheExpirationMinutes: 5);

// 주기적으로 만료된 캐시 정리
largeGraph.CleanExpiredCache();
```

## 클래스 구조

### Graph<TNode>
메인 그래프 클래스

**생성자**
- `Graph(bool isDirected, int cacheExpirationMinutes)`

**메서드**
- `AddNode(TNode node)`: 노드 추가
- `AddEdge(TNode from, TNode to, double weight)`: 간선 추가 (자동으로 모든 캐시 무효화)
- `RemoveEdge(TNode from, TNode to)`: 간선 제거 (자동으로 모든 캐시 무효화)
- `GetNeighbors(TNode node)`: 노드의 이웃 노드들 조회
- `GetAllEdges()`: 모든 간선 조회 (**캐싱됨** - 반복 호출 시 빠름)
- `FindShortestPath(TNode start, TNode end, bool useCache)`: 최단 경로 (Dijkstra, 캐싱 지원)
- `KruskalMST()`: 최소 신장 트리 (Kruskal)
- `PrimMST(TNode? startNode)`: 최소 신장 트리 (Prim)
- `PrecomputeAllPaths()`: 모든 경로 사전 계산
- `GetCacheStats()`: 경로 캐시 통계
- `CleanExpiredCache()`: 만료된 경로 캐시 정리
- `InvalidateCache()`: 경로 캐시만 무효화

### Edge<TNode>
간선 구조체

**속성**
- `From`: 시작 노드
- `To`: 도착 노드
- `Weight`: 가중치

### PathResult<TNode>
경로 탐색 결과

**속성**
- `Path`: 경로 리스트
- `TotalWeight`: 총 가중치
- `CachedAt`: 캐시 시간

### MSTResult<TNode>
MST 결과

**속성**
- `Edges`: MST 간선 리스트
- `TotalWeight`: 총 가중치

## 보조 자료구조

### PriorityQueue<T>
우선순위 큐 (Min Heap)
- Dijkstra, Prim 알고리즘에서 사용

### UnionFind<T>
Union-Find (Disjoint Set)
- Kruskal 알고리즘에서 사용
- 경로 압축 및 Union by rank 최적화

## 시간 복잡도

| 알고리즘 | 시간 복잡도 | 공간 복잡도 | 비고 |
|---------|-----------|-----------|------|
| Dijkstra | O((V + E) log V) | O(V) | 경로 캐싱 지원 |
| Kruskal MST | O(E log E) | O(V) | - |
| Prim MST | O((V + E) log V) | O(V) | - |
| GetAllEdges() | O(1) ~ O(V + E) | O(E) | 첫 호출 O(V+E), 이후 O(1) (캐싱) |

- V: 노드 수
- E: 간선 수

## 주의사항

1. **MST는 무방향 그래프에서만 동작**
   - `isDirected: true`인 경우 예외 발생

2. **캐시 메모리 관리**
   - 대용량 그래프의 경우 주기적으로 `CleanExpiredCache()` 호출
   - 필요시 `cacheExpirationMinutes`를 짧게 설정

3. **그래프 수정 시 캐시 자동 무효화**
   - `AddEdge()`, `RemoveEdge()` 호출 시 자동으로 **모든 캐시 무효화** (경로 캐시 + 엣지 리스트 캐시)
   - 그래프 구조 변경 후 캐시는 자동으로 재계산됨

4. **제네릭 타입 제약**
   - `TNode`는 `notnull`이어야 함
   - `Equals()` 및 `GetHashCode()` 올바르게 구현 필요

## 라이선스 및 기여

CommonLib 프로젝트의 일부입니다.
