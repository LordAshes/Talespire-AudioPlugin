using BepInEx;
using System;
using System.Reflection;
using ModdingTales;

namespace HolloFox
{
    public partial class AudioPlugin : BaseUnityPlugin
    {
        public static class SoftDependency
        {
            public static void Invoke(string typeName, string methodName, object[] methodParameters)
            {
                if (LogLevel > ModdingUtils.LogLevel.None)
                    _logger.LogInfo("Soft Dependency: Invoking " + methodName + "On " + typeName);
                Type t = Type.GetType(typeName);
                if (t != null)
                {
                    MethodInfo m = t.GetMethod(methodName);
                    if (m != null)
                    {
                        try
                        {
                            m.Invoke(null, methodParameters);
                        }
                        catch (Exception x)
                        {
                            if (LogLevel > ModdingUtils.LogLevel.None)
                                _logger.LogInfo("Soft Dependency: Method Invoke Failed");
                            if (LogLevel > ModdingUtils.LogLevel.None) _logger.LogError(x);
                            if (LogLevel > ModdingUtils.LogLevel.None)
                                _logger.LogInfo("Soft Dependency: Trying Fallback");
                            InvokeEx(typeName, methodName, methodParameters);
                        }
                    }
                    else
                    {
                        if (LogLevel > ModdingUtils.LogLevel.None)
                            _logger.LogInfo("Soft Dependency: Method Reference Null");
                    }
                }
                else
                {
                    if (LogLevel > ModdingUtils.LogLevel.None) _logger.LogInfo("Soft Dependency: Type Reference Null");
                }
            }

            public static void InvokeEx(string typeName, string methodName, object[] methodParameters)
            {
                if (LogLevel > ModdingUtils.LogLevel.None)
                    _logger.LogInfo("Soft Dependency: Ex Invoking " + methodName + "On " + typeName);
                Type t = Type.GetType(typeName);
                if (t != null)
                {
                    foreach (MethodInfo m in t.GetMethods())
                        if (m.Name == methodName)
                        {
                            try
                            {
                                m.Invoke(null, methodParameters);
                                return;
                            }
                            catch (Exception x)
                            {
                                if (LogLevel > ModdingUtils.LogLevel.None)
                                    _logger.LogInfo(
                                        "Soft Dependency: Ex Method Invoke Failed. Checking For Other Options");
                                if (LogLevel > ModdingUtils.LogLevel.None) _logger.LogError(x);
                                if (LogLevel > ModdingUtils.LogLevel.None)
                                    _logger.LogInfo("Soft Dependency: Checking For Other Options");
                            }
                        }
                }
                else
                {
                    if (LogLevel > ModdingUtils.LogLevel.None)
                        _logger.LogInfo("Soft Dependency: Ex Type Reference Null");
                }

                if (LogLevel > ModdingUtils.LogLevel.None)
                    _logger.LogInfo("Soft Dependency: Ex Failed To Find Suitable Invoking Method");
            }

            public static T GetProperty<T>(string typeName, string propertyName)
            {
                if (LogLevel > ModdingUtils.LogLevel.None)
                    _logger.LogInfo("Soft Dependency: GetProperty " + propertyName + "Of " + typeName);
                Type t = Type.GetType(typeName);
                if (t != null)
                {
                    PropertyInfo p = t.GetProperty(propertyName);
                    if (p != null)
                    {
                        return (T)p.GetValue(null);
                    }

                    FieldInfo f = t.GetField(propertyName);
                    if (f != null)
                    {
                        return (T)f.GetValue(null);
                    }

                    if (LogLevel > ModdingUtils.LogLevel.None)
                        _logger.LogInfo("Soft Dependency: Property/Field Not Found");
                }
                else
                {
                    if (LogLevel > ModdingUtils.LogLevel.None) _logger.LogInfo("Soft Dependency: Type Reference Null");
                }

                return default(T);
            }
        }
    }
}