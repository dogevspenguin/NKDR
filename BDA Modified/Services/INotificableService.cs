using System;

namespace BDArmory.Services
{
    public interface INotificableService<T> where T : EventArgs
    {
        event EventHandler<T> OnActionExecuted;

        void PublishEvent(T t);
    }
}
