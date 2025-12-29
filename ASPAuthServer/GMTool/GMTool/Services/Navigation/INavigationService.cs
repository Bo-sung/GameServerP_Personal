using System.Windows.Controls;

namespace GMTool.Services.Navigation
{
    public interface INavigationService
    {
        /// <summary>
        /// Frame 설정
        /// </summary>
        void SetFrame(Frame frame);

        /// <summary>
        /// 페이지로 이동
        /// </summary>
        void NavigateTo<T>() where T : Page, new();

        /// <summary>
        /// 뒤로 가기
        /// </summary>
        void GoBack();

        /// <summary>
        /// 앞으로 가기
        /// </summary>
        void GoForward();

        /// <summary>
        /// 뒤로 갈 수 있는지 여부
        /// </summary>
        bool CanGoBack { get; }

        /// <summary>
        /// 앞으로 갈 수 있는지 여부
        /// </summary>
        bool CanGoForward { get; }
    }
}
