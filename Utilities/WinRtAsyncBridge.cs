using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace JukeboxSpotify
{
    internal static class WinRtAsyncBridge
    {
        private static readonly MethodInfo AsTaskMethod = ResolveAsTaskMethod();

        public static bool IsAvailable => AsTaskMethod != null;

        public static async Task<object> AwaitResultAsync(object asyncOperation)
        {
            if (asyncOperation == null)
            {
                return null;
            }

            if (AsTaskMethod == null)
            {
                throw new PlatformNotSupportedException("The WinRT async bridge is unavailable in the current runtime.");
            }

            Type asyncInterfaceType = asyncOperation
                .GetType()
                .GetInterfaces()
                .FirstOrDefault(type => type.IsGenericType && type.Name.StartsWith("IAsyncOperation`1", StringComparison.Ordinal));

            if (asyncInterfaceType == null)
            {
                throw new InvalidOperationException($"The WinRT operation type '{asyncOperation.GetType().FullName}' does not expose IAsyncOperation<TResult>.");
            }

            Type resultType = asyncInterfaceType.GetGenericArguments()[0];
            object taskObject = AsTaskMethod.MakeGenericMethod(resultType).Invoke(null, new[] { asyncOperation });
            Task task = (Task)taskObject;
            await task.ConfigureAwait(false);
            return taskObject.GetType().GetProperty("Result")?.GetValue(taskObject);
        }

        private static MethodInfo ResolveAsTaskMethod()
        {
            Type windowsRuntimeExtensionsType = Type.GetType(
                "System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeSystemExtensions, System.Runtime.WindowsRuntime",
                throwOnError: false);

            if (windowsRuntimeExtensionsType == null)
            {
                return null;
            }

            return windowsRuntimeExtensionsType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "AsTask" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.IsGenericType &&
                    method.GetParameters()[0].ParameterType.GetGenericTypeDefinition().Name == "IAsyncOperation`1");
        }
    }
}