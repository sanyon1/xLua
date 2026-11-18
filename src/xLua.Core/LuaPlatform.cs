/*
 * Tencent is pleased to support the open source community by making xLua available.
 * Copyright (C) 2016 THL A29 Limited, a Tencent company. All rights reserved.
 * Licensed under the MIT License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at
 * http://opensource.org/licenses/MIT
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
*/

using System;

namespace XLua
{
    // What the environment hosting the runtime tells it about itself. The runtime has no engine
    // dependency; an integration such as the Unity package sets these before the first LuaEnv
    // is created and adds its own loaders and globals through LuaEnv.AddIniter.
    public static class LuaPlatform
    {
        // Returns false for an object the host has destroyed although C# still holds it.
        // Such objects are released from the Lua side and read back as nil.
        public static Func<object, bool> ObjectValidChecker = null;

        // The host cannot create generic instantiations at run time that were not compiled ahead of time.
        public static bool Aot = false;

        // Only adapt Lua functions to generic Action/Func shapes that are safe without a JIT:
        // no value-type results and at most four parameters.
        public static bool ConservativeGenericDelegates = false;
    }
}
