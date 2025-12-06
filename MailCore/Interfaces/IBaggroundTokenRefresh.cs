namespace MailCore.Interfaces
{
    public interface IBackgroundTokenRefresher
    {
        void Start();
        void Stop();
    }
}
