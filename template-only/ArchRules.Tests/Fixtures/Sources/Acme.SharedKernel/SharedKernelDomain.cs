using System.Reflection;

namespace Acme.SharedKernel;

public static class SharedKernelDomain
{
    public static Assembly Assembly => typeof(SharedKernelDomain).Assembly;
}
