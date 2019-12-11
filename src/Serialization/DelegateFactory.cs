// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Amqp.Serialization
{
    using System;
    using System.Reflection;

    /// <summary>
    /// A factory to create delegates.
    /// </summary>
    public interface IDelegateFactory
    {
        /// <summary>
        /// Creates a delegate to get or set a field.
        /// </summary>
        /// <typeparam name="T">A Func for getter or an Action for setter.</typeparam>
        /// <param name="fieldInfo">The field info.</param>
        /// <returns>The delegate.</returns>
        T Create<T>(FieldInfo fieldInfo);

        /// <summary>
        /// Creates a delegate to invoke a method.
        /// </summary>
        /// <typeparam name="T">The delegate.</typeparam>
        /// <param name="methodInfo">The method info.</param>
        /// <returns>The delegate.</returns>
        /// <remarks>The delegate type must match the method info.</remarks>
        T Create<T>(MethodInfo methodInfo);
    }

    class DelegateFactory : IDelegateFactory
    {
        T IDelegateFactory.Create<T>(FieldInfo fieldInfo)
        {
            // The default implementation of field getter/setter
            // uses reflection and it is slow. It is provided for
            // back-comp.
            if (typeof(T) == typeof(Func<object, object>))
            {
                Func<object, object> del = obj => fieldInfo.GetValue(obj);
                return (T)(object)del;
            }
            else if (typeof(T) == typeof(Action<object, object>))
            {
                Action<object, object> del = (obj, val) => fieldInfo.SetValue(obj, val);
                return (T)(object)del;
            }

            throw new NotSupportedException(typeof(T).Name);
        }

        T IDelegateFactory.Create<T>(MethodInfo methodInfo)
        {
            ParameterInfo[] parameters = methodInfo.GetParameters();
            Validate(methodInfo, parameters, typeof(T));

            if (methodInfo.ReturnType == typeof(void))
            {
                switch (parameters.Length)
                {
                    case 0:
                    {
                        var del = CreateDelegate<Action<object>>(methodInfo, "CreateAction", methodInfo.DeclaringType);
                        return (T)(object)del;
                    }
                    case 1:
                    {
                        var del = CreateDelegate<Action<object, object>>(methodInfo, "CreateAction1", methodInfo.DeclaringType, parameters[0].ParameterType);
                        return (T)(object)del;
                    }
                    case 2:
                    {
                        var del = CreateDelegate<Action<object, object, object>>(methodInfo, "CreateAction2", methodInfo.DeclaringType, parameters[0].ParameterType, parameters[1].ParameterType);
                        return (T)(object)del;
                    }
                    default:
                        throw new InvalidOperationException();
                }
            }
            else
            {
                switch (parameters.Length)
                {
                    case 0:
                    {
                        var del = CreateDelegate<Func<object, object>>(methodInfo, "CreateFunc", methodInfo.DeclaringType, methodInfo.ReturnType);
                        return (T)(object)del;
                    }
                    default:
                        throw new InvalidOperationException();
                }

            }
        }

        static void Validate(MethodInfo methodInfo, ParameterInfo[] parameters, Type delType)
        {
            if (!delType.IsGenericType)
            {
                throw new NotSupportedException($"{methodInfo.Name}: The requested delegate type '{delType.Name}' is not a generic type.");
            }

            foreach (var arg in delType.GenericTypeArguments)
            {
                if (arg != typeof(object))
                {
                    throw new NotSupportedException($"{methodInfo.Name}: The requested delegate type '{delType.Name}' must have object argument type.");
                }
            }

            if (methodInfo.ReturnType == typeof(void))
            {
                if (parameters.Length > 2)
                {
                    throw new NotSupportedException($"{methodInfo.Name}: the maximum number of parameters allowed is 2.");
                }

                if (!delType.Name.StartsWith("Action"))
                {
                    throw new NotSupportedException($"{methodInfo.Name}: The requested delegate type '{delType.Name}' is not an Action.");
                }

                if (delType.GenericTypeArguments.Length != parameters.Length + 1)
                {
                    throw new NotSupportedException($"{methodInfo.Name}: The requested delegate type '{delType.Name}' has incorrect number of parameters.");
                }
            }
            else
            {
                if (!delType.Name.StartsWith("Func"))
                {
                    throw new NotSupportedException($"{methodInfo.Name}: The requested delegate type '{delType.Name}' is not a Func.");
                }

                if (parameters.Length > 0)
                {
                    throw new NotSupportedException($"{methodInfo.Name}: no parameters are allowed in Func delegate.");
                }

                if (delType.GenericTypeArguments.Length != parameters.Length + 2)
                {
                    throw new NotSupportedException($"{methodInfo.Name}: The requested delegate type '{delType.Name}' has incorrect number of parameters.");
                }
            }
        }

        static T CreateDelegate<T>(MethodInfo methodInfo, string name, params Type[] typeArgs)
        {
            MethodInfo action = typeof(DelegateFactory).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo generic = action.MakeGenericMethod(typeArgs);
            return (T)generic.Invoke(null, new object[] { methodInfo });
        }

        delegate void RefAction<TTarget>(ref TTarget target);

        delegate void RefAction<TTarget, TParam>(ref TTarget target, TParam param);

        delegate void RefAction<TTarget, TParam1, TParam2>(ref TTarget target, TParam1 param1, TParam2 param2);

        delegate TReturn RefFunc<TTarget, TReturn>(ref TTarget target);

        static Action<object> CreateAction<TTarget>(MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType.IsValueType)
            {
                var del = (RefAction<TTarget>)methodInfo.CreateDelegate(typeof(RefAction<TTarget>));
                return (object obj) =>
                {
                    TTarget target = (TTarget)obj;
                    del(ref target);
                };
            }
            else
            {
                var del = (Action<TTarget>)methodInfo.CreateDelegate(typeof(Action<TTarget>));
                return (object obj) => del((TTarget)obj);
            }
        }

        static Action<object, object> CreateAction1<TTarget, TParam>(MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType.IsValueType)
            {
                var del = (RefAction<TTarget, TParam>)methodInfo.CreateDelegate(typeof(RefAction<TTarget, TParam>));
                return (object o, object p) =>
                {
                    TTarget target = (TTarget)o;
                    del(ref target, (TParam)p);
                };
            }
            else
            {
                var del = (Action<TTarget, TParam>)methodInfo.CreateDelegate(typeof(Action<TTarget, TParam>));
                return (object obj, object p) => del((TTarget)obj, (TParam)p);
            }
        }

        static Action<object, object, object> CreateAction2<TTarget, TParam1, TParam2>(MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType.IsValueType)
            {
                var del = (RefAction<TTarget, TParam1, TParam2>)methodInfo.CreateDelegate(typeof(RefAction<TTarget, TParam1, TParam2>));
                return (object o, object p1, object p2) =>
                {
                    TTarget target = (TTarget)o;
                    del(ref target, (TParam1)p1, (TParam2)p2);
                };
            }
            else
            {
                var del = (Action<TTarget, TParam1, TParam2>)methodInfo.CreateDelegate(typeof(Action<TTarget, TParam1, TParam2>));
                return (object obj, object p1, object p2) => del((TTarget)obj, (TParam1)p1, (TParam2)p2);
            }
        }

        static Func<object, object> CreateFunc<TTarget, TReturn>(MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType.IsValueType)
            {
                var del = (RefFunc<TTarget, TReturn>)methodInfo.CreateDelegate(typeof(RefFunc<TTarget, TReturn>));
                return (object o) =>
                {
                    TTarget target = (TTarget)o;
                    return del(ref target);
                };
            }
            else
            {
                var del = (Func<TTarget, TReturn>)methodInfo.CreateDelegate(typeof(Func<TTarget, TReturn>));
                return (object obj) => del((TTarget)obj);
            }
        }
    }
}
