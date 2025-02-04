using System.Reflection;
using Trish.API.Interfaces;

namespace Trish.API.Extensions
{
    public static class MinimalApiExtensions
    {
        public static void MapEndpoint(this WebApplication app)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var classes = assemblies
                .Distinct()
                .SelectMany(x =>
                {
                    try
                    {
                        return x.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t != null);
                    }
                })
                .Where(x => typeof(IApiModule).IsAssignableFrom(x) && !x.IsAbstract);

            foreach (var assembly in classes)
            {
                var instance = Activator.CreateInstance(assembly!) as IApiModule;
                instance?.MapEndpoint(app);
            }
        }
    }
}
