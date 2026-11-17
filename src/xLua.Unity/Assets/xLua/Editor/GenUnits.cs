/*
 * Tencent is pleased to support the open source community by making xLua available.
 * Copyright (C) 2016 THL A29 Limited, a Tencent company. All rights reserved.
 * Licensed under the MIT License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at
 * http://opensource.org/licenses/MIT
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions and limitations under the License.
*/

#if !XLUA_GENERAL
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace CSObjectWrapEditor
{
    // One folder of generated code and the assembly it is compiled into.
    public class GenUnit
    {
        // null when the assembly that compiles the default path is unknown
        public string AssemblyName;
        // absolute, ends with '/'
        public string Path;
        public bool IsDefault;

        // names of the assemblies this one references; null means assume it sees everything
        internal HashSet<string> references;
        internal GenUnitPlan plan;

        // Whether the generated code for this type belongs in this unit.
        public bool Owns(Type type)
        {
            var owner = plan.OwnerOf(type);
            return IsDefault ? owner == null : owner == this;
        }

        // Whether code in this unit can name the type.
        public bool Sees(Type type)
        {
            if (references == null)
            {
                return true;
            }
            foreach (var name in plan.AssembliesOf(type))
            {
                if (name == AssemblyName || references.Contains(name))
                {
                    continue;
                }
                if (!plan.scriptAssemblies.Contains(name) && GenUnitPlan.IsBaseClassLibrary(name))
                {
                    continue;
                }
                return false;
            }
            return true;
        }
    }

    // Decides which unit each type's generated code goes to. Types of an assembly definition under
    // Assets that references xLua go to a folder next to its .asmdef, everything else to the default path.
    public class GenUnitPlan
    {
        public List<GenUnit> Units = new List<GenUnit>();
        public GenUnit Default;

        internal HashSet<string> scriptAssemblies = new HashSet<string>();
        Dictionary<string, GenUnit> unitByAssembly = new Dictionary<string, GenUnit>();
        Dictionary<Type, GenUnit> ownerCache = new Dictionary<Type, GenUnit>();
        Dictionary<Type, string[]> assembliesCache = new Dictionary<Type, string[]>();

        internal static bool IsBaseClassLibrary(string name)
        {
            return name == "mscorlib" || name == "netstandard" || name == "System" || name.StartsWith("System.") || name == "Microsoft.CSharp";
        }

        static void collectAssemblies(Type type, HashSet<string> names)
        {
            while (type.HasElementType)
            {
                type = type.GetElementType();
            }
            if (type.IsGenericParameter)
            {
                return;
            }
            names.Add(type.Assembly.GetName().Name);
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    collectAssemblies(arg, names);
                }
            }
        }

        // The assemblies a piece of code has to reference to name this type.
        internal string[] AssembliesOf(Type type)
        {
            string[] ret;
            if (!assembliesCache.TryGetValue(type, out ret))
            {
                var names = new HashSet<string>();
                collectAssemblies(type, names);
                ret = names.ToArray();
                assembliesCache.Add(type, ret);
            }
            return ret;
        }

        // The non-default unit the type belongs to, or null for the default one.
        internal GenUnit OwnerOf(Type type)
        {
            GenUnit owner;
            if (ownerCache.TryGetValue(type, out owner))
            {
                return owner;
            }
            var candidates = new List<GenUnit>();
            foreach (var name in AssembliesOf(type))
            {
                GenUnit unit;
                if (unitByAssembly.TryGetValue(name, out unit))
                {
                    candidates.Add(unit);
                }
            }
            // With several candidates, the one that can see all the others is the only place it compiles.
            owner = candidates.FirstOrDefault(candidate => candidate.Sees(type));
            ownerCache.Add(type, owner);
            return owner;
        }

        static string normalize(string path)
        {
            return System.IO.Path.GetFullPath(path).Replace('\\', '/');
        }

        public static GenUnitPlan Create(string defaultPath, bool perAssembly)
        {
            var plan = new GenUnitPlan();
            plan.Default = new GenUnit() { Path = defaultPath, IsDefault = true, plan = plan };
            plan.Units.Add(plan.Default);

#if UNITY_2019_3_OR_NEWER
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (var assembly in assemblies)
            {
                plan.scriptAssemblies.Add(assembly.name);
            }
            Func<Assembly, HashSet<string>> referencesOf = (assembly) =>
            {
                var names = new HashSet<string>(assembly.assemblyReferences.Select(r => r.name));
                foreach (var compiled in assembly.compiledAssemblyReferences)
                {
                    names.Add(System.IO.Path.GetFileNameWithoutExtension(compiled));
                }
                return names;
            };

            string projectRoot = normalize(Application.dataPath + "/..") + "/";
            string fullDefaultPath = normalize(defaultPath) + "/";
            if (fullDefaultPath.StartsWith(projectRoot))
            {
                string dll = CompilationPipeline.GetAssemblyNameFromScriptPath(fullDefaultPath.Substring(projectRoot.Length) + "XLuaGenAutoRegister.cs");
                if (!string.IsNullOrEmpty(dll))
                {
                    plan.Default.AssemblyName = System.IO.Path.GetFileNameWithoutExtension(dll);
                    var defaultAssembly = assemblies.FirstOrDefault(a => a.name == plan.Default.AssemblyName);
                    if (defaultAssembly != null)
                    {
                        plan.Default.references = referencesOf(defaultAssembly);
                    }
                }
            }

            if (perAssembly)
            {
                string xluaAssembly = typeof(XLua.LuaEnv).Assembly.GetName().Name;
                foreach (var assembly in assemblies)
                {
                    if (assembly.name == plan.Default.AssemblyName || assembly.name == xluaAssembly)
                    {
                        continue;
                    }
                    string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                    // predefined assemblies have no folder of their own and packages are read-only
                    if (string.IsNullOrEmpty(asmdef) || !asmdef.Replace('\\', '/').StartsWith("Assets/"))
                    {
                        continue;
                    }
                    if (!assembly.assemblyReferences.Any(r => r.name == xluaAssembly))
                    {
                        continue;
                    }
                    var unit = new GenUnit()
                    {
                        AssemblyName = assembly.name,
                        Path = normalize(System.IO.Path.GetDirectoryName(asmdef)) + "/" + GeneratorConfig.assembly_gen_folder + "/",
                        references = referencesOf(assembly),
                        plan = plan,
                    };
                    plan.Units.Add(unit);
                    plan.unitByAssembly.Add(assembly.name, unit);
                }
            }
#endif
            return plan;
        }
    }
}
#endif
