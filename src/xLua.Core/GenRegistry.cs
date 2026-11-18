/*
 * Tencent is pleased to support the open source community by making xLua available.
 * Copyright (C) 2016 THL A29 Limited, a Tencent company. All rights reserved.
 * Licensed under the MIT License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at
 * http://opensource.org/licenses/MIT
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
*/

#if USE_UNI_LUA
using RealStatePtr = UniLua.ILuaState;
#else
using RealStatePtr = System.IntPtr;
#endif

using System;
using System.Collections.Generic;
using System.Reflection;

namespace XLua
{
    // The entry point generated code registers itself through. A generated assembly references
    // this one, never the other way round, so everything it provides comes in as typed delegates.
    // Any number of assemblies can register; what they provide is merged.
    public static class GenRegistry
    {
        public delegate Delegate DelegateCreator(DelegateBridge bridge);
        public delegate bool TryArrayGet(Type type, RealStatePtr L, ObjectTranslator translator, object obj, int index);
        public delegate bool TryArraySet(Type type, RealStatePtr L, ObjectTranslator translator, object obj, int array_idx, int obj_idx);

        static readonly object sync = new object();
        // One per registered assembly: the static class holding its __Gen_Delegate_Imp functions.
        static volatile Type[] delegateBridgeHolders = new Type[0];
        static readonly Dictionary<Type, DelegateCreator> delegateCreators = new Dictionary<Type, DelegateCreator>();
        static MethodInfo[] bridgeMethods = null;

        public static bool Registered
        {
            get
            {
                return delegateBridgeHolders.Length > 0;
            }
        }

        // The assemblies generated code was compiled into, in registration order.
        public static Assembly[] GenAssemblies
        {
            get
            {
                var holders = delegateBridgeHolders;
                var assemblies = new List<Assembly>();
                for (int i = 0; i < holders.Length; i++)
                {
                    if (!assemblies.Contains(holders[i].Assembly))
                    {
                        assemblies.Add(holders[i].Assembly);
                    }
                }
                return assemblies.ToArray();
            }
        }

        // Must run before the first LuaEnv is created. Registering the same holder again is a no-op.
        // When two assemblies provide the same thing, the one registered first is used.
        public static void Register(Type delegateBridgeHolder,
            Dictionary<Type, DelegateCreator> delegateCreators,
            Dictionary<Type, IEnumerable<MethodInfo>> extensionMethodMap,
            TryArrayGet tryArrayGet,
            TryArraySet tryArraySet,
            params Action<LuaEnv, ObjectTranslator>[] initers)
        {
            if (delegateBridgeHolder == null)
            {
                throw new ArgumentNullException("delegateBridgeHolder");
            }
            lock (sync)
            {
                if (Array.IndexOf(delegateBridgeHolders, delegateBridgeHolder) >= 0)
                {
                    return;
                }

                for (int i = 0; i < initers.Length; i++)
                {
                    LuaEnv.AddIniter(initers[i]);
                }

                if (delegateCreators != null)
                {
                    foreach (var kv in delegateCreators)
                    {
                        if (!GenRegistry.delegateCreators.ContainsKey(kv.Key))
                        {
                            GenRegistry.delegateCreators.Add(kv.Key, kv.Value);
                        }
                    }
                }

                if (extensionMethodMap != null)
                {
                    var merged = InternalGlobals.extensionMethodMap == null
                        ? new Dictionary<Type, IEnumerable<MethodInfo>>()
                        : new Dictionary<Type, IEnumerable<MethodInfo>>(InternalGlobals.extensionMethodMap);
                    foreach (var kv in extensionMethodMap)
                    {
                        IEnumerable<MethodInfo> existing;
                        if (merged.TryGetValue(kv.Key, out existing))
                        {
                            var methods = new List<MethodInfo>(existing);
                            methods.AddRange(kv.Value);
                            merged[kv.Key] = methods;
                        }
                        else
                        {
                            merged.Add(kv.Key, kv.Value);
                        }
                    }
                    InternalGlobals.extensionMethodMap = merged;
                }

                if (tryArrayGet != null)
                {
                    var previous = InternalGlobals.genTryArrayGetPtr;
                    InternalGlobals.genTryArrayGetPtr = previous == null ? tryArrayGet
                        : (type, L, translator, obj, index) => previous(type, L, translator, obj, index) || tryArrayGet(type, L, translator, obj, index);
                }
                if (tryArraySet != null)
                {
                    var previous = InternalGlobals.genTryArraySetPtr;
                    InternalGlobals.genTryArraySetPtr = previous == null ? tryArraySet
                        : (type, L, translator, obj, array_idx, obj_idx) => previous(type, L, translator, obj, array_idx, obj_idx) || tryArraySet(type, L, translator, obj, array_idx, obj_idx);
                }

                var holders = new Type[delegateBridgeHolders.Length + 1];
                Array.Copy(delegateBridgeHolders, holders, delegateBridgeHolders.Length);
                holders[holders.Length - 1] = delegateBridgeHolder;
                bridgeMethods = null;
                delegateBridgeHolders = holders;
            }
        }

        // The delegate generated for exactly this type, or null.
        internal static Delegate CreateDelegate(DelegateBridge bridge, Type delegateType)
        {
            DelegateCreator creator;
            lock (sync)
            {
                if (!delegateCreators.TryGetValue(delegateType, out creator))
                {
                    return null;
                }
            }
            return creator(bridge);
        }

        // A generated bridge function whose signature fits the delegate, for delegate types that
        // were not generated themselves (the hotfix injector creates such types). Null if none.
        internal static MethodInfo FindBridgeMethod(MethodInfo delegateMethod)
        {
            MethodInfo[] methods;
            lock (sync)
            {
                if (bridgeMethods == null)
                {
                    var found = new List<MethodInfo>();
                    for (int i = 0; i < delegateBridgeHolders.Length; i++)
                    {
                        var candidates = delegateBridgeHolders[i].GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                        for (int j = 0; j < candidates.Length; j++)
                        {
                            if (candidates[j].Name.StartsWith("__Gen_Delegate_Imp"))
                            {
                                found.Add(candidates[j]);
                            }
                        }
                    }
                    bridgeMethods = found.ToArray();
                }
                methods = bridgeMethods;
            }

            ParameterInfo[] delegateParams = delegateMethod.GetParameters();
            for (int i = 0; i < methods.Length; i++)
            {
                if (delegateMethod.ReturnType != methods[i].ReturnType)
                {
                    continue;
                }
                // The first parameter is the bridge itself.
                ParameterInfo[] bridgeParams = methods[i].GetParameters();
                if (delegateParams.Length != bridgeParams.Length - 1)
                {
                    continue;
                }
                bool match = true;
                for (int j = 0; j < delegateParams.Length; j++)
                {
                    if (delegateParams[j].ParameterType != bridgeParams[j + 1].ParameterType || delegateParams[j].IsOut != bridgeParams[j + 1].IsOut)
                    {
                        match = false;
                        break;
                    }
                }
                if (!match)
                {
                    continue;
                }
                int last = delegateParams.Length - 1;
                if (last < 0 || delegateParams[last].IsDefined(typeof(ParamArrayAttribute), false) == bridgeParams[last + 1].IsDefined(typeof(ParamArrayAttribute), false))
                {
                    return methods[i];
                }
            }
            return null;
        }

#if XLUA_GENERAL
        // Without Unity there is no RuntimeInitializeOnLoadMethod to run the generated entry points,
        // so look them up once for hosts that do not call XLua.XLuaGenRegister.Register() themselves.
        static bool searched = false;
        internal static void EnsureRegistered()
        {
            // a host that runs the generated entry points itself has already registered them
            if (searched || Registered)
            {
                return;
            }
            searched = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("XLua.XLuaGenRegister");
                if (type == null)
                {
                    continue;
                }
                var register = type.GetMethod("Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (register != null)
                {
                    register.Invoke(null, null);
                }
            }
        }
#endif
    }
}
