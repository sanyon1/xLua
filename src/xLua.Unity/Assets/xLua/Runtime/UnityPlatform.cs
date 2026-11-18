/*
 * Tencent is pleased to support the open source community by making xLua available.
 * Copyright (C) 2016 THL A29 Limited, a Tencent company. All rights reserved.
 * Licensed under the MIT License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at
 * http://opensource.org/licenses/MIT
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
*/

using LuaAPI = XLua.LuaDLL.Lua;
using RealStatePtr = System.IntPtr;
using LuaCSFunction = XLua.LuaDLL.lua_CSFunction;

using System;
using System.IO;

namespace XLua
{
    // Everything Unity-specific the runtime needs. The runtime itself (XLua.Core) knows nothing
    // about the engine; this plugs the Unity behaviour into it before the first LuaEnv exists.
    public static class UnityPlatform
    {
        static bool installed = false;

        // The engine calls this on its own. Code that creates a LuaEnv earlier than that can call
        // it directly; a second call does nothing.
#if UNITY_2019_2_OR_NEWER
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        public static void Install()
        {
            if (installed)
            {
                return;
            }
            installed = true;

            LuaPlatform.ObjectValidChecker = ObjectValidCheck;
            LuaPlatform.ConservativeGenericDelegates = true;
#if ENABLE_IL2CPP
            LuaPlatform.Aot = true;
#endif
            LuaEnv.AddIniter(Init);
        }

        // A destroyed UnityEngine.Object still exists as a C# object but compares equal to null.
        static bool ObjectValidCheck(object obj)
        {
            return (!(obj is UnityEngine.Object)) || ((obj as UnityEngine.Object) != null);
        }

        static void Init(LuaEnv luaenv, ObjectTranslator translator)
        {
            RealStatePtr L = luaenv.L;

            LuaAPI.lua_pushstdcallcfunction(L, Print);
            if (0 != LuaAPI.xlua_setglobal(L, "print"))
            {
                throw new Exception("call xlua_setglobal fail!");
            }

            luaenv.AddSearcher(LoadFromResource, 4);
            luaenv.AddSearcher(LoadFromStreamingAssetsPath, -1);

#if !UNITY_WSA || UNITY_EDITOR
            luaenv.Alias(typeof(Type), "System.MonoType");
#endif
        }

        [MonoPInvokeCallback(typeof(LuaCSFunction))]
        internal static int Print(RealStatePtr L)
        {
            try
            {
                int n = LuaAPI.lua_gettop(L);
                string s = String.Empty;

                if (0 != LuaAPI.xlua_getglobal(L, "tostring"))
                {
                    return LuaAPI.luaL_error(L, "can not get tostring in print:");
                }

                for (int i = 1; i <= n; i++)
                {
                    LuaAPI.lua_pushvalue(L, -1);  /* function to be called */
                    LuaAPI.lua_pushvalue(L, i);   /* value to print */
                    if (0 != LuaAPI.lua_pcall(L, 1, 1, 0))
                    {
                        return LuaAPI.lua_error(L);
                    }
                    s += LuaAPI.lua_tostring(L, -1);

                    if (i != n) s += "\t";

                    LuaAPI.lua_pop(L, 1);  /* pop result */
                }
                UnityEngine.Debug.Log("LUA: " + s);
                return 0;
            }
            catch (System.Exception e)
            {
                return LuaAPI.luaL_error(L, "c# exception in print:" + e);
            }
        }

        [MonoPInvokeCallback(typeof(LuaCSFunction))]
        internal static int LoadFromResource(RealStatePtr L)
        {
            try
            {
                string filename = LuaAPI.lua_tostring(L, 1).Replace('.', '/') + ".lua";

                // Load with Unity3D resources
                UnityEngine.TextAsset file = (UnityEngine.TextAsset)UnityEngine.Resources.Load(filename);
                if (file == null)
                {
                    LuaAPI.lua_pushstring(L, string.Format(
                        "\n\tno such resource '{0}'", filename));
                }
                else
                {
                    if (LuaAPI.xluaL_loadbuffer(L, file.bytes, file.bytes.Length, "@" + filename) != 0)
                    {
                        return LuaAPI.luaL_error(L, String.Format("error loading module {0} from resource, {1}",
                            LuaAPI.lua_tostring(L, 1), LuaAPI.lua_tostring(L, -1)));
                    }
                }

                return 1;
            }
            catch (System.Exception e)
            {
                return LuaAPI.luaL_error(L, "c# exception in LoadFromResource:" + e);
            }
        }

        [MonoPInvokeCallback(typeof(LuaCSFunction))]
        internal static int LoadFromStreamingAssetsPath(RealStatePtr L)
        {
            try
            {
                string filename = LuaAPI.lua_tostring(L, 1).Replace('.', '/') + ".lua";
                var filepath = UnityEngine.Application.streamingAssetsPath + "/" + filename;
#if UNITY_ANDROID && !UNITY_EDITOR
                UnityEngine.WWW www = new UnityEngine.WWW(filepath);
                while (true)
                {
                    if (www.isDone || !string.IsNullOrEmpty(www.error))
                    {
                        System.Threading.Thread.Sleep(50); //�Ƚ�hacker������
                        if (!string.IsNullOrEmpty(www.error))
                        {
                            LuaAPI.lua_pushstring(L, string.Format(
                               "\n\tno such file '{0}' in streamingAssetsPath!", filename));
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("load lua file from StreamingAssets is obsolete, filename:" + filename);
                            if (LuaAPI.xluaL_loadbuffer(L, www.bytes, www.bytes.Length , "@" + filename) != 0)
                            {
                                return LuaAPI.luaL_error(L, String.Format("error loading module {0} from streamingAssetsPath, {1}",
                                    LuaAPI.lua_tostring(L, 1), LuaAPI.lua_tostring(L, -1)));
                            }
                        }
                        break;
                    }
                }
#else
                if (File.Exists(filepath))
                {
                    // string text = File.ReadAllText(filepath);
                    var bytes = File.ReadAllBytes(filepath);

                    UnityEngine.Debug.LogWarning("load lua file from StreamingAssets is obsolete, filename:" + filename);
                    if (LuaAPI.xluaL_loadbuffer(L, bytes, bytes.Length, "@" + filename) != 0)
                    {
                        return LuaAPI.luaL_error(L, String.Format("error loading module {0} from streamingAssetsPath, {1}",
                            LuaAPI.lua_tostring(L, 1), LuaAPI.lua_tostring(L, -1)));
                    }
                }
                else
                {
                    LuaAPI.lua_pushstring(L, string.Format(
                        "\n\tno such file '{0}' in streamingAssetsPath!", filename));
                }
#endif
                return 1;
            }
            catch (System.Exception e)
            {
                return LuaAPI.luaL_error(L, "c# exception in LoadFromStreamingAssetsPath:" + e);
            }
        }
    }
}
