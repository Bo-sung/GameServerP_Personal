# WPF GM Tool 성능 최적화 가이드

## 개요
중저사양 PC에서도 원활하게 동작하는 플랫한 디자인의 GM Tool 제작 가이드

---

## UI 디자인 원칙

### ✅ 사용할 것

#### 1. 플랫 디자인 요소
- **단색 배경** (그라데이션 최소화)
- **얇은 테두리** (1px Stroke)
- **단순한 아이콘** (벡터 기반, Path 사용)
- **명확한 구분선** (불필요한 그림자 제거)

```xml
<!-- ✅ GOOD: 플랫한 카드 -->
<Border Background="White"
        BorderBrush="#E0E0E0"
        BorderThickness="1"
        CornerRadius="4"
        Padding="16">
    <StackPanel>
        <TextBlock Text="총 사용자 수" FontSize="14" Foreground="#666" />
        <TextBlock Text="1,234" FontSize="28" FontWeight="SemiBold" />
    </StackPanel>
</Border>
```

#### 2. 기본 WPF 컨트롤
- **TextBlock**: 텍스트 표시
- **Button**: 기본 버튼 (스타일 커스텀)
- **DataGrid**: 사용자 목록
- **TextBox**: 입력 필드
- **Border**: 레이아웃 구분

---

### ❌ 피할 것

#### 1. 무거운 시각 효과
```xml
<!-- ❌ BAD: Drop Shadow -->
<Border>
    <Border.Effect>
        <DropShadowEffect BlurRadius="20" /> <!-- CPU 과부하 -->
    </Border.Effect>
</Border>

<!-- ✅ GOOD: 테두리로 대체 -->
<Border BorderBrush="#E0E0E0" BorderThickness="1" />
```

#### 2. 복잡한 애니메이션
```xml
<!-- ❌ BAD: 불필요한 Storyboard -->
<Button>
    <Button.Triggers>
        <EventTrigger RoutedEvent="MouseEnter">
            <BeginStoryboard>
                <Storyboard>
                    <DoubleAnimation Duration="0:0:0.3" ... />
                    <ColorAnimation Duration="0:0:0.3" ... />
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Button.Triggers>
</Button>

<!-- ✅ GOOD: 단순한 색상 변경 -->
<Button>
    <Button.Style>
        <Style TargetType="Button">
            <Setter Property="Background" Value="#F5F5F5" />
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#E0E0E0" />
                </Trigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

#### 3. Blur 효과
```xml
<!-- ❌ BAD: BlurEffect (매우 무거움) -->
<Border>
    <Border.Effect>
        <BlurEffect Radius="10" />
    </Border.Effect>
</Border>

<!-- ✅ GOOD: 반투명 오버레이 -->
<Border Background="#80000000" /> <!-- 80 = 50% 투명도 -->
```

---

## ModernWpfUI 최적화 설정

### 1. 불필요한 테마 효과 비활성화

```xml
<!-- App.xaml -->
<Application>
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- ModernWpfUI 기본 테마 -->
                <ui:ThemeResources>
                    <!-- ✅ Acrylic(블러) 비활성화 -->
                    <ui:ThemeResources.AccentColor>#0078D4</ui:ThemeResources.AccentColor>
                </ui:ThemeResources>

                <!-- ❌ Acrylic 사용하지 않음 -->
                <!-- <ui:AcrylicResources /> -->
            </ResourceDictionary.MergedDictionaries>

            <!-- ✅ 플랫 스타일 오버라이드 -->
            <Style TargetType="Button" BasedOn="{StaticResource DefaultButtonStyle}">
                <Setter Property="Effect" Value="{x:Null}" />
                <Setter Property="BorderThickness" Value="1" />
            </Style>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### 2. 단순화된 커스텀 스타일

```xml
<!-- Resources/Styles/FlatStyles.xaml -->
<ResourceDictionary>
    <!-- 플랫 카드 스타일 -->
    <Style x:Key="FlatCardStyle" TargetType="Border">
        <Setter Property="Background" Value="White" />
        <Setter Property="BorderBrush" Value="#E0E0E0" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="4" />
        <Setter Property="Padding" Value="16" />
    </Style>

    <!-- 플랫 버튼 스타일 -->
    <Style x:Key="FlatButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="#F5F5F5" />
        <Setter Property="BorderBrush" Value="#D0D0D0" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="12,8" />
        <Setter Property="Cursor" Value="Hand" />

        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#E8E8E8" />
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
                <Setter Property="Background" Value="#D0D0D0" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- 액센트 버튼 -->
    <Style x:Key="AccentButtonStyle" TargetType="Button" BasedOn="{StaticResource FlatButtonStyle}">
        <Setter Property="Background" Value="#0078D4" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="BorderBrush" Value="#0078D4" />

        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#006ABC" />
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>
```

---

## DataGrid 성능 최적화

### 1. 가상화 활성화 (기본값이지만 명시)

```xml
<DataGrid ItemsSource="{Binding Users}"
          AutoGenerateColumns="False"
          IsReadOnly="True"
          VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          EnableRowVirtualization="True"
          EnableColumnVirtualization="True">
    <!-- 컬럼 정의 -->
</DataGrid>
```

### 2. 페이지네이션 적용 (한 번에 표시 제한)

```csharp
// UserListViewModel.cs
public class UserListViewModel : ObservableObject
{
    private const int PAGE_SIZE = 20; // ✅ 한 페이지에 20개만 표시

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        var response = await _userService.GetUsersAsync(
            page: CurrentPage,
            pageSize: PAGE_SIZE  // 대량 데이터 로드 방지
        );

        Users = new ObservableCollection<User>(response.Users);
    }
}
```

### 3. 불필요한 컬럼 제거

```xml
<!-- ❌ BAD: 너무 많은 컬럼 -->
<DataGrid>
    <DataGrid.Columns>
        <DataGridTextColumn ... />
        <DataGridTextColumn ... />
        <!-- 10개 이상의 컬럼 -->
    </DataGrid.Columns>
</DataGrid>

<!-- ✅ GOOD: 필수 컬럼만 -->
<DataGrid>
    <DataGrid.Columns>
        <DataGridTextColumn Header="ID" Binding="{Binding Id}" Width="60" />
        <DataGridTextColumn Header="사용자명" Binding="{Binding Username}" Width="150" />
        <DataGridTextColumn Header="이메일" Binding="{Binding Email}" Width="*" />
        <DataGridTextColumn Header="상태" Binding="{Binding IsActive}" Width="80" />
        <DataGridTemplateColumn Header="액션" Width="100">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <Button Content="상세" Command="{Binding DataContext.ViewDetailCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}" />
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
    </DataGrid.Columns>
</DataGrid>
```

---

## 메모리 관리

### 1. 이미지 최적화

```xml
<!-- ❌ BAD: 원본 이미지 직접 로드 -->
<Image Source="/Images/logo.png" />

<!-- ✅ GOOD: 크기 제한 -->
<Image Source="/Images/logo.png" Width="32" Height="32" />

<!-- ✅ BETTER: BitmapImage로 미리 리사이징 -->
<Image>
    <Image.Source>
        <BitmapImage UriSource="/Images/logo.png" DecodePixelWidth="32" />
    </Image.Source>
</Image>
```

### 2. 이벤트 핸들러 정리

```csharp
// ViewModel이 Dispose될 때 이벤트 해제
public class UserListViewModel : ObservableObject, IDisposable
{
    private Timer _refreshTimer;

    public UserListViewModel()
    {
        _refreshTimer = new Timer(30000); // 30초마다 새로고침
        _refreshTimer.Elapsed += OnTimerElapsed;
        _refreshTimer.Start();
    }

    public void Dispose()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Elapsed -= OnTimerElapsed; // ✅ 이벤트 해제
            _refreshTimer.Dispose();
        }
    }
}
```

---

## 네트워킹 최적화

### 1. 불필요한 API 호출 방지

```csharp
// ❌ BAD: 매번 API 호출
public async Task LoadStatisticsAsync()
{
    Statistics = await _statisticsService.GetStatisticsAsync();
}

// ✅ GOOD: 캐싱 추가
private DateTime _lastRefresh;
private ServerStatistics _cachedStatistics;
private const int CACHE_DURATION_SECONDS = 30;

public async Task LoadStatisticsAsync()
{
    if (_cachedStatistics != null &&
        (DateTime.Now - _lastRefresh).TotalSeconds < CACHE_DURATION_SECONDS)
    {
        Statistics = _cachedStatistics; // 캐시 사용
        return;
    }

    _cachedStatistics = await _statisticsService.GetStatisticsAsync();
    _lastRefresh = DateTime.Now;
    Statistics = _cachedStatistics;
}
```

### 2. 병렬 로딩 (필요 시)

```csharp
// ✅ GOOD: 독립적인 데이터는 병렬로 로드
public async Task InitializeAsync()
{
    var statisticsTask = _statisticsService.GetStatisticsAsync();
    var usersTask = _userService.GetUsersAsync(page: 1, pageSize: 20);

    await Task.WhenAll(statisticsTask, usersTask);

    Statistics = await statisticsTask;
    Users = new ObservableCollection<User>((await usersTask).Users);
}
```

---

## UI 렌더링 최적화

### 1. Binding Mode 최적화

```xml
<!-- ❌ BAD: 불필요한 TwoWay -->
<TextBlock Text="{Binding Username, Mode=TwoWay}" />

<!-- ✅ GOOD: OneWay (읽기 전용) -->
<TextBlock Text="{Binding Username, Mode=OneWay}" />

<!-- OneTime: 변경되지 않는 데이터 -->
<TextBlock Text="{Binding UserId, Mode=OneTime}" />
```

### 2. UpdateSourceTrigger 최적화

```xml
<!-- ❌ BAD: 키 입력마다 업데이트 -->
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />

<!-- ✅ GOOD: 포커스 잃을 때 업데이트 -->
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=LostFocus}" />

<!-- 또는 검색 버튼 사용 -->
<StackPanel Orientation="Horizontal">
    <TextBox x:Name="SearchBox" Width="200" />
    <Button Content="검색" Command="{Binding SearchCommand}"
            CommandParameter="{Binding ElementName=SearchBox, Path=Text}" />
</StackPanel>
```

### 3. Freezable 객체 고정

```csharp
// Brush, Pen 등 변경되지 않는 리소스는 Freeze()
public static class FlatColors
{
    public static readonly Brush Primary = CreateFrozenBrush("#0078D4");
    public static readonly Brush Background = CreateFrozenBrush("#F5F5F5");

    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(hex)
        );
        brush.Freeze(); // ✅ 성능 향상
        return brush;
    }
}
```

---

## 플랫 UI 컬러 팔레트

```xml
<!-- Resources/Colors.xaml -->
<ResourceDictionary>
    <!-- 메인 컬러 -->
    <SolidColorBrush x:Key="PrimaryBrush" Color="#0078D4" />
    <SolidColorBrush x:Key="PrimaryHoverBrush" Color="#006ABC" />

    <!-- 배경 -->
    <SolidColorBrush x:Key="BackgroundBrush" Color="#FAFAFA" />
    <SolidColorBrush x:Key="CardBackgroundBrush" Color="#FFFFFF" />

    <!-- 텍스트 -->
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="#212121" />
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="#666666" />
    <SolidColorBrush x:Key="TextDisabledBrush" Color="#BDBDBD" />

    <!-- 테두리 -->
    <SolidColorBrush x:Key="BorderBrush" Color="#E0E0E0" />
    <SolidColorBrush x:Key="DividerBrush" Color="#EEEEEE" />

    <!-- 상태 -->
    <SolidColorBrush x:Key="SuccessBrush" Color="#4CAF50" />
    <SolidColorBrush x:Key="WarningBrush" Color="#FF9800" />
    <SolidColorBrush x:Key="ErrorBrush" Color="#F44336" />
</ResourceDictionary>
```

---

## 대시보드 예제 (플랫 디자인)

```xml
<!-- Views/Pages/DashboardPage.xaml -->
<Page Background="{StaticResource BackgroundBrush}">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="24" MaxWidth="1200">

            <!-- 헤더 -->
            <TextBlock Text="대시보드"
                       FontSize="24"
                       FontWeight="SemiBold"
                       Foreground="{StaticResource TextPrimaryBrush}"
                       Margin="0,0,0,24" />

            <!-- 통계 카드 그리드 -->
            <UniformGrid Rows="2" Columns="3">
                <!-- 카드 1: 총 사용자 -->
                <Border Style="{StaticResource FlatCardStyle}" Margin="0,0,12,12">
                    <StackPanel>
                        <TextBlock Text="총 사용자"
                                   FontSize="13"
                                   Foreground="{StaticResource TextSecondaryBrush}" />
                        <TextBlock Text="{Binding Statistics.TotalUsers}"
                                   FontSize="32"
                                   FontWeight="SemiBold"
                                   Foreground="{StaticResource TextPrimaryBrush}"
                                   Margin="0,4,0,0" />
                    </StackPanel>
                </Border>

                <!-- 카드 2: 활성 사용자 -->
                <Border Style="{StaticResource FlatCardStyle}" Margin="0,0,12,12">
                    <StackPanel>
                        <TextBlock Text="활성 사용자"
                                   FontSize="13"
                                   Foreground="{StaticResource TextSecondaryBrush}" />
                        <TextBlock Text="{Binding Statistics.ActiveUsers}"
                                   FontSize="32"
                                   FontWeight="SemiBold"
                                   Foreground="{StaticResource SuccessBrush}"
                                   Margin="0,4,0,0" />
                    </StackPanel>
                </Border>

                <!-- 카드 3: 온라인 사용자 -->
                <Border Style="{StaticResource FlatCardStyle}" Margin="0,0,0,12">
                    <StackPanel>
                        <TextBlock Text="현재 온라인"
                                   FontSize="13"
                                   Foreground="{StaticResource TextSecondaryBrush}" />
                        <TextBlock Text="{Binding Statistics.OnlineUsers}"
                                   FontSize="32"
                                   FontWeight="SemiBold"
                                   Foreground="{StaticResource PrimaryBrush}"
                                   Margin="0,4,0,0" />
                    </StackPanel>
                </Border>

                <!-- 나머지 카드들... -->
            </UniformGrid>

            <!-- 새로고침 버튼 -->
            <Button Content="새로고침"
                    Style="{StaticResource AccentButtonStyle}"
                    Command="{Binding RefreshCommand}"
                    HorizontalAlignment="Left"
                    Margin="0,16,0,0" />
        </StackPanel>
    </ScrollViewer>
</Page>
```

---

## 성능 측정

### 1. WPF Performance Profiler 사용

```
Visual Studio → Debug → Performance Profiler → WPF Performance
```

### 2. 렌더링 성능 확인

```csharp
// App.xaml.cs에 추가
protected override void OnStartup(StartupEventArgs e)
{
    #if DEBUG
    // 프레임 레이트 표시
    Application.Current.MainWindow.Title += " - FPS: " + RenderingEventArgs.RenderingTime;
    #endif

    base.OnStartup(e);
}
```

---

## 체크리스트

### 필수 최적화
- [ ] DropShadowEffect 제거
- [ ] BlurEffect 제거
- [ ] DataGrid 가상화 활성화
- [ ] 페이지네이션 구현 (20개/페이지)
- [ ] Binding Mode 최적화 (OneWay, OneTime)
- [ ] 이미지 DecodePixelWidth 설정
- [ ] 불필요한 애니메이션 제거

### 플랫 디자인
- [ ] 단색 배경 사용
- [ ] 1px 테두리 사용
- [ ] 그라데이션 제거
- [ ] 단순한 색상 전환만 사용
- [ ] Acrylic 효과 비활성화

### 메모리 관리
- [ ] 타이머/이벤트 핸들러 정리
- [ ] API 호출 캐싱 (30초)
- [ ] Freezable 객체 Freeze()
- [ ] WeakReference 사용 (필요 시)

---

## 권장 시스템 요구사항

### 최소 사양
- **CPU**: Intel Core i3 이상
- **RAM**: 4GB
- **OS**: Windows 10 (1809 이상)
- **.NET**: .NET 8.0 Runtime

### 권장 사양
- **CPU**: Intel Core i5 이상
- **RAM**: 8GB
- **OS**: Windows 10/11
- **.NET**: .NET 8.0 Runtime

---

## 추가 최적화 팁

### 1. Lazy Loading
```csharp
// 사용자 상세 정보는 클릭 시에만 로드
[RelayCommand]
private async Task ViewUserDetailAsync(int userId)
{
    // 여기서 추가 정보 로드
    var userDetail = await _userService.GetUserByIdAsync(userId);
    _navigationService.NavigateToUserDetail(userDetail);
}
```

### 2. Debounce 검색
```csharp
private Timer _searchDebounceTimer;

partial void OnSearchTextChanged(string value)
{
    _searchDebounceTimer?.Dispose();
    _searchDebounceTimer = new Timer(500); // 500ms 후 검색
    _searchDebounceTimer.Elapsed += async (s, e) =>
    {
        await LoadUsersAsync();
    };
    _searchDebounceTimer.Start();
}
```

### 3. Background Thread 활용
```csharp
// 무거운 계산은 백그라운드 스레드에서
await Task.Run(() =>
{
    // 무거운 작업
});

// UI 업데이트는 Dispatcher에서
Application.Current.Dispatcher.Invoke(() =>
{
    Users = newUsers;
});
```
