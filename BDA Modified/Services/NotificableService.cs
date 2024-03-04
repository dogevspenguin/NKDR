using System;

namespace BDArmory.Services
{
    public abstract class NotificableService<T> : INotificableService<T> where T : EventArgs
    {
        public event EventHandler<T> OnActionExecuted;

        public void PublishEvent(T t)
        {
            OnActionExecuted?.Invoke(this, t);
        }
    }
}
