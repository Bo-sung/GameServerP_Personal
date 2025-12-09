namespace LobbyServer.Utils
{
    /// <summary>
    /// 스레드 세이프한 싱글톤 베이스 클래스
    /// </summary>
    /// <typeparam name="T">싱글톤으로 만들 클래스 타입</typeparam>
    public abstract class SingletonBase<T> where T : class
    {
        // volatile 키워드는 멀티스레드 환경에서 변수가 최신 값을 유지하도록 보장합니다
        private static volatile T _instance;

        // 객체 잠금을 위한 동기화 객체
        private static readonly object _lock = new object();

        /// <summary>
        /// 싱글톤 인스턴스를 가져옵니다
        /// </summary>
        public static T Instance
        {
            get
            {
                // 더블 체크 락킹 패턴 사용
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            // 리플렉션을 통해 생성자 호출
                            _instance = Activator.CreateInstance(typeof(T), true) as T;

                            // 초기화 메서드 호출 (자식 클래스에서 구현 가능)
                            (_instance as SingletonBase<T>)?.Initialize();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 생성자
        /// </summary>
        protected SingletonBase()
        {
            // 생성자는 protected로 설정하여 외부에서 직접 인스턴스화 할 수 없게 함
        }

        /// <summary>
        /// 초기화 메서드 - 필요시 자식 클래스에서 오버라이드
        /// </summary>
        protected virtual void Initialize() { }
    }
}