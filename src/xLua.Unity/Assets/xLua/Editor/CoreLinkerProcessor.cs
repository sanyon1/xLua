/*
 * Tencent is pleased to support the open source community by making xLua available.
 * Copyright (C) 2016 THL A29 Limited, a Tencent company. All rights reserved.
 * Licensed under the MIT License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at
 * http://opensource.org/licenses/MIT
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
*/

#if UNITY_2019_3_OR_NEWER
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace XLua
{
    // Much of XLua.Core is only ever reached from Lua, by name, so managed stripping would remove
    // code that scripts rely on. A link.xml inside a package is not picked up, so the build is
    // told here to keep the whole assembly.
    class CoreLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder { get { return 0; } }

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string path = Path.GetFullPath("Temp/XLuaCoreLink.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "<linker>\n    <assembly fullname=\"XLua.Core\" preserve=\"all\"/>\n</linker>\n");
            return path;
        }

        // Still part of the interface in some editor versions.
        public void OnBeforeRun(BuildReport report, UnityLinkerBuildPipelineData data)
        {
        }

        public void OnAfterRun(BuildReport report, UnityLinkerBuildPipelineData data)
        {
        }
    }
}
#endif
