using System.Reflection;

namespace FuncAuthz.Extensions;

internal class AttributeExtension
{
    internal static (TAttribute? attribute, AttributeTargets attributeTargets, int parent)? GetAttribute<TAttribute>(MethodInfo methodInfo)
        where TAttribute : Attribute
    {
        return GetAttribute<TAttribute>(methodInfo, 0);
    }

    private static (TAttribute? attribute, AttributeTargets attributeTargets, int parent)? GetAttribute<TAttribute>
        (MethodInfo methodInfo, int level)
        where TAttribute : Attribute
    {
        TAttribute? methodAttribute = (TAttribute?)Attribute.GetCustomAttribute(methodInfo, typeof(TAttribute), false);
        if (methodAttribute != null)
        {
            return (methodAttribute, AttributeTargets.Method, level);
        }

        if (methodInfo.DeclaringType is not null)
        {
            var classAttribute = (TAttribute?)Attribute.GetCustomAttribute(methodInfo.DeclaringType, typeof(TAttribute), false);
            if (classAttribute != null)
            {
                return (classAttribute, AttributeTargets.Class, level);
            }
        }

        var baseMethod = methodInfo.DeclaringType!.BaseType?.GetMethod(methodInfo.Name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy,
            methodInfo.GetParameters().Select(x => x.ParameterType).ToArray());
        if (baseMethod is not null)
        {
            if (baseMethod?.DeclaringType != methodInfo.DeclaringType)
            {
                var baseMethodAttribute = GetAttribute<TAttribute>(baseMethod, ++level);
                if (baseMethodAttribute?.attribute != null)
                {
                    return (baseMethodAttribute?.attribute, baseMethodAttribute!.Value.attributeTargets, baseMethodAttribute!.Value.parent);
                }
            }
        }

        return null;
    }
}
