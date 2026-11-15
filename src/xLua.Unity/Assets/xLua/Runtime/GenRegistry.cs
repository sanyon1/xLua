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
    // The entry point generated code registers itself through. The generated assembly references
    // this one, never the other way round, so everything it provides comes in as typed delegates.
    public static class GenRegistry
    {
        public delegate DelegateBridgeBase DelegateBridgeCreator(int reference, LuaEnv luaenv);
        public delegate bool TryArrayGet(Type type, RealStatePtr L, ObjectTranslator translator, object obj, int index);
        public delegate bool TryArraySet(Type type, RealStatePtr L, ObjectTranslator translator, object obj, int array_idx, int obj_idx);

        internal static volatile DelegateBridgeCreator delegateBridgeCreator = null;

        public static bool Registered
        {
            get
            {
                return delegateBridgeCreator != null;
            }
        }

        // The assembly the generated code was compiled into, null if nothing registered.
        public static Assembly GenAssembly
        {
            get
            {
                var creator = delegateBridgeCreator;
                return creator == null ? null : creator.Method.Module.Assembly;
            }
        }

        // Must run before the first LuaEnv is created. Calling it again is a no-op.
        public static void Register(DelegateBridgeCreator delegateBridgeCreator,
            Dictionary<Type, IEnumerable<MethodInfo>> extensionMethodMap,
            TryArrayGet tryArrayGet,
            TryArraySet tryArraySet,
            params Action<LuaEnv, ObjectTranslator>[] initers)
        {
            if (delegateBridgeCreator == null)
            {
                throw new ArgumentNullException("delegateBridgeCreator");
            }
            lock (typeof(GenRegistry))
            {
                if (Registered)
                {
                    return;
                }
                for (int i = 0; i < initers.Length; i++)
                {
                    LuaEnv.AddIniter(initers[i]);
                }
                InternalGlobals.extensionMethodMap = extensionMethodMap;
                InternalGlobals.genTryArrayGetPtr = tryArrayGet;
                InternalGlobals.genTryArraySetPtr = tryArraySet;
                GenRegistry.delegateBridgeCreator = delegateBridgeCreator;
            }
        }

#if XLUA_GENERAL
        // Without Unity there is no RuntimeInitializeOnLoadMethod to run the generated entry point,
        // so look it up once for hosts that do not call XLua.XLuaGenRegister.Register() themselves.
        internal static void EnsureRegistered()
        {
            if (Registered)
            {
                return;
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("XLua.XLuaGenRegister");
                if (type == null)
                {
                    continue;
                }
                var register = type.GetMethod("Register", BindingFlags.Static | BindingFlags.Public);
                if (register != null)
                {
                    register.Invoke(null, null);
                    return;
                }
            }
        }
#endif
    }
}
