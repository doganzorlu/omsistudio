using System;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public interface IUiDispatcher
{
    bool CheckAccess();
    Task InvokeAsync(Action action);
}
