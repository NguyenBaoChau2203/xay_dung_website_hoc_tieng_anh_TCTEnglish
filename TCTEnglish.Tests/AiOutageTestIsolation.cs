using System.Runtime.CompilerServices;
using TCTEnglish.Services.AI.Resilience;

namespace TCTEnglish.Tests;

internal static class AiOutageTestIsolation
{
    [ModuleInitializer]
    internal static void SuppressSharedGuard()
    {
        AiOutageAmbient.SuppressProcessGuard = true;
    }
}
