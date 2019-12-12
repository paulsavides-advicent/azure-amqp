// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Test.Microsoft.Azure.Amqp
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using global::Microsoft.Azure.Amqp.Serialization;

    /// <summary>
    /// A delegate factory based on DynamicMethod and IL emit.
    /// </summary>
    public class DynamicDelegateFactory : IDelegateFactory
    {
        public T Create<T>(FieldInfo fieldInfo)
        {
            if (typeof(T) == typeof(Func<object, object>))
            {
                Func<object, object> del = CreateGetter(fieldInfo);
                return (T)(object)del;
            }
            else if (typeof(T) == typeof(Action<object, object>))
            {
                Action<object, object> del = CreateSetter(fieldInfo);
                return (T)(object)del;
            }

            throw new NotSupportedException(typeof(T).Name);
        }

        public T Create<T>(MethodInfo methodInfo)
        {
            return CreateMethodDelegate<T>(methodInfo);
        }

        static Func<object, object> CreateGetter(FieldInfo fieldInfo)
        {
            DynamicMethod method = new DynamicMethod(GetAccessorName(true, fieldInfo.Name), typeof(object), new[] { typeof(object) }, true);
            ILGenerator generator = method.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            EmitTypeConversion(generator, fieldInfo.DeclaringType, true);
            generator.Emit(OpCodes.Ldfld, fieldInfo);
            if (fieldInfo.FieldType.GetTypeInfo().IsValueType)
            {
                generator.Emit(OpCodes.Box, fieldInfo.FieldType);
            }

            generator.Emit(OpCodes.Ret);

            return (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>));
        }

        static Action<object, object> CreateSetter(FieldInfo fieldInfo)
        {
            DynamicMethod method = new DynamicMethod(GetAccessorName(false, fieldInfo.Name), typeof(void), new[] { typeof(object), typeof(object) }, true);
            ILGenerator generator = method.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            EmitTypeConversion(generator, fieldInfo.DeclaringType, true);
            generator.Emit(OpCodes.Ldarg_1);
            EmitTypeConversion(generator, fieldInfo.FieldType, false);
            generator.Emit(OpCodes.Stfld, fieldInfo);
            generator.Emit(OpCodes.Ret);

            return (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>));
        }

        static T CreateMethodDelegate<T>(MethodInfo methodInfo)
        {
            Type returnType = methodInfo.ReturnType != typeof(void) ? typeof(object) : typeof(void);
            int paramsCount = 1 + methodInfo.GetParameters().Length;
            Type[] paramsType = Enumerable.Range(1, paramsCount).Select(i => typeof(object)).ToArray();
            string name = "Dynamic" + methodInfo.DeclaringType.Name + methodInfo.Name;

            DynamicMethod method = new DynamicMethod(name, returnType, paramsType, true);
            ILGenerator generator = method.GetILGenerator();
            if (!methodInfo.IsStatic)
            {
                // load first argument as the target
                generator.Emit(OpCodes.Ldarg_0);
                EmitTypeConversion(generator, methodInfo.DeclaringType, true);
            }

            // load arguments
            Type[] parameters = methodInfo.GetParameters()
                .Select(p => p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType)
                .ToArray();
            for (int i = 0; i < parameters.Length; i++)
            {
                Type paramType = parameters[i];
                switch (i)
                {
                    case 0: generator.Emit(OpCodes.Ldarg_1); break;
                    case 1: generator.Emit(OpCodes.Ldarg_2); break;
                    case 2: generator.Emit(OpCodes.Ldarg_3); break;
                    default: generator.Emit(OpCodes.Ldarg, i); break;
                }

                EmitTypeConversion(generator, paramType, false);
            }

            EmitCall(generator, methodInfo);

            if (methodInfo.ReturnType != typeof(void) &&
                methodInfo.ReturnType.GetTypeInfo().IsValueType)
            {
                generator.Emit(OpCodes.Box, methodInfo.ReturnType);
            }

            generator.Emit(OpCodes.Ret);

            try
            {
                return (T)(object)method.CreateDelegate(typeof(T));
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"The specified delegate type '{typeof(T).Name}' does not match the method '{method.Name}'.");
            }
        }

        static void EmitTypeConversion(ILGenerator generator, Type castType, bool isContainer)
        {
            if (castType == typeof(object))
            {
            }
            else if (castType.GetTypeInfo().IsValueType)
            {
                generator.Emit(isContainer ? OpCodes.Unbox : OpCodes.Unbox_Any, castType);
            }
            else
            {
                generator.Emit(OpCodes.Castclass, castType);
            }
        }

        static void EmitCall(ILGenerator generator, MethodInfo method)
        {
            OpCode opcode = (method.IsStatic || method.DeclaringType.GetTypeInfo().IsValueType) ? OpCodes.Call : OpCodes.Callvirt;
            generator.EmitCall(opcode, method, null);
        }

        static string GetAccessorName(bool isGetter, string name)
        {
            return (isGetter ? "get_" : "set_") + name;
        }

    }
}
