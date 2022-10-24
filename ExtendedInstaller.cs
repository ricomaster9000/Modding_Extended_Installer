using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Mono.Cecil.Cil;
using Newtonsoft.Json;

// ReSharper disable All

namespace ExtendedInstaller
{
    public static class ExtendedInstaller
    {

        public static void Main(string[] args)
        {
            try
            {
                var assemblyPath = Path.GetFullPath(args[0]);
                var assemblyReplaceAddWithPath = Path.GetFullPath(args[2]);

                // make backup
                string currentDirectory = Path.GetDirectoryName(assemblyPath);
                string fullPathOnly = Path.GetFullPath(currentDirectory);
                string backupPath = fullPathOnly + "\\" + Path.GetFileNameWithoutExtension(assemblyPath) + "_backup.dll";
                if (!File.Exists(backupPath))
                {
                    File.Copy(assemblyPath, backupPath);
                    Console.WriteLine("BACKUP MADE AT -> " + backupPath);
                }

                using var alterer = new Alterer(assemblyPath, assemblyReplaceAddWithPath);

                Console.WriteLine("Running Extended Installer");
                var result = alterer.Run(args[1]);


                alterer.Write(assemblyPath);
                PrintResult(assemblyPath, result);

                Dictionary<String, object> Extended_Install_Configurations = new Dictionary<string, object>();
                Extended_Install_Configurations.Add("extended_installation_completed", true);
                Extended_Install_Configurations.Add("extended_installation_main_dll_file_size", new System.IO.FileInfo(assemblyPath).Length);
                File.WriteAllText("..\\Extended_Install_Configurations.txt", JsonConvert.SerializeObject(Extended_Install_Configurations));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Thread.Sleep(100000);
            }
        }

        private static void PrintResult(string path, Alterer.AltererResult result)
        {
            Console.WriteLine($"Publicized - {path}");
            Console.WriteLine("Publicize result - ");
            Console.WriteLine($"\tTypes - {result.Types}");
            Console.WriteLine($"\tNestedTypes - {result.NestedTypes}");
            Console.WriteLine($"\tEvents - {result.Events}");
            Console.WriteLine($"\tFields - {result.Fields}");
            Console.WriteLine($"\tMethods - {result.Methods}");
            Console.WriteLine("\tProperties -");
            Console.WriteLine($"\t\tSetters - {result.Property_Setters}");
            Console.WriteLine($"\t\tGetters - {result.Property_Getters}");

            Console.WriteLine($"Replaced - {path}");
            Console.WriteLine("Replaced result - ");
            Console.WriteLine($"\tMethods Replaced - {result.MethodsReplaced}");
        }

        public static ReaderParameters GetReaderParameters() => new(ReadingMode.Immediate)
        {
            InMemory = true
        };
    }

    public sealed class Alterer : IDisposable
    {
        public static Dictionary<string,AssemblyDefinition> _additionalAssembliesToIncludeAllTypesWhenReplacing = new Dictionary<string, AssemblyDefinition>();

        public static Dictionary<AssemblyDefinition, Collection<TypeDefinition>> types = new Dictionary<AssemblyDefinition, Collection<TypeDefinition>>();

        public static Dictionary<MethodDefinition, MethodDefinition> MethodToReplacementMethod = new Dictionary<MethodDefinition, MethodDefinition>();

        public static Dictionary<AssemblyDefinition, List<MethodDefinition>> methods = new Dictionary<AssemblyDefinition, List<MethodDefinition>>();

        public static Dictionary<MethodDefinition, bool> methodReplaced = new Dictionary<MethodDefinition, bool>();
        public static String[] methodsToReplaceArray = new String[] { };

        public sealed class AltererResult
        {
            public uint Types;
            public uint NestedTypes;
            public uint Events;
            public uint Fields;
            public uint Methods;
            public uint MethodsReplaced;

            public uint Property_Setters;
            public uint Property_Getters;

            public void BumpTypes() => ++Types;
            public void BumpNestedTypes() => ++NestedTypes;
            public void BumpEvents() => ++Events;
            public void BumpFields() => ++Fields;
            public void BumpMethods() => ++Methods;

            public void BumpReplaceMethods() => ++MethodsReplaced;
            public void BumpPropertySetters() => ++Property_Setters;
            public void BumpPropertyGetters() => ++Property_Getters;
        }

        private readonly AssemblyDefinition _assembly;
        private readonly AssemblyDefinition _assemblyReplaceWith;

        public Alterer(string pathTargetAssembly, string pathReplaceWithAssembly)
        {
            _assembly = MainUtility.loadAssembly(pathTargetAssembly);
            _assemblyReplaceWith = MainUtility.loadAssembly(pathReplaceWithAssembly);
        }

        public AltererResult Run(String methodsOrFieldsToMakePublic)
        {
            var result = new AltererResult();
            Console.WriteLine("Publicizing stuff");
            DoPublicize(result, methodsOrFieldsToMakePublic.Split(','));
            //Console.WriteLine("Replacing/Patching stuff");
            //Console.WriteLine("Methods to replace/patch: " + methodsToReplace);
            //methodsToReplaceArray = methodsToReplace.Split(',');
            //DoReplace(result, methodsToReplaceArray);
            return result;
        }

        public void Write(string path)
        {
            _assembly.Write(path);
        }

        private void DoPublicize(AltererResult value, String[] methodsOrFieldsToMakePublic)
        {
            //DoPublicizeTypes(value);
            //DoPublicizeNestedTypes(value);
            DoPublicizeFields(value, methodsOrFieldsToMakePublic);
            // Publicize events before publicizing methods cuz events are literally two methods & one field
            //DoFixupEvents(value);
            // Publicize properties before publicizing methods cuz setters/getters are methods
            DoPublicizePropertySetters(value, methodsOrFieldsToMakePublic);
            DoPublicizePropertyGetters(value, methodsOrFieldsToMakePublic);

            DoPublicizeMethods(value, methodsOrFieldsToMakePublic);

        }

        private void DoReplace(AltererResult value, String[] methodsToReplace)
        {
            try
            {
                DoReplaceMethods(value, methodsToReplace);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Thread.Sleep(100000);
            }
        }

        #region Utility

        private Collection<TypeDefinition> GetTypes()
        {
            if (types.ContainsKey(_assembly) && types[_assembly].Count > 0)
            {
                return types[_assembly];
            }

            var coll = new Collection<TypeDefinition>();

            void AddTypes(Collection<TypeDefinition> definitions)
            {
                for (var z = 0; z < definitions.Count; z++)
                {
                    var definition = definitions[z];
                    coll.Add(definition);
                    AddTypes(definition.NestedTypes);
                }
            }

            AddTypes(_assembly.MainModule.Types);
            return types[_assembly] = coll;
        }

        private Collection<TypeDefinition> GetTypes(AssemblyDefinition assemblyDefinition)
        {
            if (types.ContainsKey(_assembly) && types[_assembly].Count > 0)
                return types[_assembly];

            var coll = new Collection<TypeDefinition>();

            void AddTypes(Collection<TypeDefinition> definitions)
            {
                for (var z = 0; z < definitions.Count; z++)
                {
                    var definition = definitions[z];
                    coll.Add(definition);
                    AddTypes(definition.NestedTypes);
                }
            }

            AddTypes(assemblyDefinition.MainModule.Types);
            types[assemblyDefinition] = coll;
            return coll;
        }

        private Collection<TypeDefinition> GetTypes(AssemblyDefinition assemblyDefinition, String forNamespace)
        {
            if (types.ContainsKey(assemblyDefinition) && types[assemblyDefinition].Count > 0)
                return types[assemblyDefinition];

            var coll = new Collection<TypeDefinition>();

            void AddTypes(Collection<TypeDefinition> definitions)
            {
                for (var z = 0; z < definitions.Count; z++)
                {
                    if (definitions[z].Namespace.EndsWith(forNamespace))
                    {
                        var definition = definitions[z];
                        coll.Add(definition);
                        AddTypes(definition.NestedTypes);
                    }
                }
            }

            AddTypes(assemblyDefinition.MainModule.Types);
            types[assemblyDefinition] = coll;
            return coll;
        }

        private List<MethodDefinition> GetMethods(AssemblyDefinition assemblyDefinition)
        {
            if (methods.ContainsKey(assemblyDefinition) && methods[assemblyDefinition].Count > 0)
            {
                return methods[assemblyDefinition];
            }

            methods[assemblyDefinition] = new List<MethodDefinition>();
            List<MethodDefinition> methodsFound = new List<MethodDefinition>();
            foreach (TypeDefinition type in GetTypes(assemblyDefinition))
            {
                methods[assemblyDefinition].AddRange(type.Methods);
            }

            return methods[assemblyDefinition];
        }

        private List<MethodDefinition> GetMethods(AssemblyDefinition assemblyDefinition, string forNamespace)
        {
            if (methods.ContainsKey(assemblyDefinition) && methods[assemblyDefinition].Count > 0)
            {
                return methods[assemblyDefinition];
            }

            methods[assemblyDefinition] = new List<MethodDefinition>();
            foreach (TypeDefinition type in GetTypes(assemblyDefinition, forNamespace))
            {
                methods[assemblyDefinition].AddRange(type.Methods);
            }

            return methods[assemblyDefinition];
        }

        private MethodDefinition GetMethodReplacement(MethodDefinition method)
        {
            MethodDefinition replacement;
            if (MethodToReplacementMethod.Count <= 0)
            {
                List<MethodDefinition> replaceWithMethods = GetMethods(_assemblyReplaceWith, "methodreplacements");
                Console.WriteLine(replaceWithMethods[0].Name);
                foreach (var methodDefinition in GetMethods(_assembly))
                {
                    MethodDefinition replaceWith = replaceWithMethods.Find(m =>
                        m.Name.StartsWith(methodDefinition.Name) &&
                        m.Name.EndsWith(methodDefinition.DeclaringType.Name));
                    if (replaceWith != null)
                    {
                        Console.WriteLine(methodDefinition.DeclaringType.Name);
                        MethodToReplacementMethod.Add(methodDefinition, replaceWith);
                    }
                }
            }

            return method != null && MethodToReplacementMethod.ContainsKey(method)
                ? MethodToReplacementMethod[method]
                : null;
        }

        private List<TypeDefinition> getAllTypeDefinitionsFromExternalAssembliesThatMustBeImported()
        {
            List<TypeDefinition> types = new List<TypeDefinition>();
            foreach (KeyValuePair<string,AssemblyDefinition> element in _additionalAssembliesToIncludeAllTypesWhenReplacing)
            {
                types.AddRange(GetTypes(element.Value));
            }
            return types;
        }

        private List<MemberReference> getAllMemberReferencesFromExternalAssembliesThatMustBeImported()
        {
            List<MemberReference> result = new List<MemberReference>();
            foreach (KeyValuePair<string, AssemblyDefinition> element in
                     _additionalAssembliesToIncludeAllTypesWhenReplacing)
            {
                result.AddRange(element.Value.MainModule.GetMemberReferences());
            }

            return result;
        }

        private void Processor<T>(Collection<T> values, Predicate<T> filter, Action<T> processor, Action bump)
        {
            for (var z = 0; z < values.Count; z++)
            {
                var d = values[z];
                if (filter(d))
                {
                    processor(d);
                    bump();
                }
            }
        }

        private void ArrayProcessor<T, R>(Collection<T> values, Func<T, Collection<R>> getter,
            Action<Collection<R>> processor)
        {
            for (var z = 0; z < values.Count; z++)
            {
                var d = values[z];
                var coll = getter(d);
                processor(coll);
            }
        }

        #endregion

        #region Do

        private void DoPublicizeTypes(AltererResult value, String[] methodsOrFieldsToMakePublic) =>
            Processor<TypeDefinition>(GetTypes(),
                (t) => !t.IsNested && !t.IsPublic,
                (t) => t.IsPublic = true,
                value.BumpTypes);

        private void DoPublicizeNestedTypes(AltererResult value, String[] methodsOrFieldsToMakePublic) =>
            Processor<TypeDefinition>(GetTypes(),
                (nt) => nt.IsNested && !nt.IsNestedPublic,
                (nt) => nt.IsNestedPublic = true,
                value.BumpNestedTypes);

        private void DoPublicizeFields(AltererResult value, String[] methodsOrFieldsToMakePublic) =>
            ArrayProcessor<TypeDefinition, FieldDefinition>(GetTypes(),
                (t) => t.Fields,
                (fs) => Processor<FieldDefinition>(fs,
                    (f) => !f.IsPublic && methodsOrFieldsToMakePublic.Contains(f.Name),
                    (f) => f.IsPublic = true,
                    value.BumpFields));

        private void DoPublicizeMethods(AltererResult value, String[] methodsOrFieldsToMakePublic) =>
            ArrayProcessor<TypeDefinition, MethodDefinition>(GetTypes(),
                (t) => t.Methods,
                (ms) => Processor<MethodDefinition>(ms,
                    (m) => !m.IsPublic && methodsOrFieldsToMakePublic.Contains(m.Name),
                    (m) => m.IsPublic = true,
                    value.BumpMethods));

        private void DoPublicizePropertySetters(AltererResult value, String[] methodsOrFieldsToMakePublic) =>
            ArrayProcessor<TypeDefinition, PropertyDefinition>(GetTypes(),
                (t) => t.Properties,
                (ps) => Processor<PropertyDefinition>(ps,
                    (p) => p is { SetMethod: { IsPublic: false } } && methodsOrFieldsToMakePublic.Contains(p.Name),
                    (p) => p.SetMethod.IsPublic = true,
                    value.BumpPropertySetters));

        private void DoPublicizePropertyGetters(AltererResult value, String[] methodsOrFieldsToMakePublic) =>
            ArrayProcessor<TypeDefinition, PropertyDefinition>(GetTypes(),
                (t) => t.Properties,
                (ps) => Processor<PropertyDefinition>(ps,
                    (p) => p is { GetMethod: { IsPublic: false } } && methodsOrFieldsToMakePublic.Contains(p.Name),
                    (p) => p.GetMethod.IsPublic = true,
                    value.BumpPropertyGetters));

        private void DoReplaceMethods(AltererResult value, string[] methodsToReplace) =>
            ArrayProcessor<TypeDefinition, MethodDefinition>(GetTypes(_assembly),
                (t) => t.Methods,
                (ms) => Processor<MethodDefinition>(ms,
                    (m) => methodsToReplace.Contains(m.Name) && GetMethodReplacement(m) != null,
                    (m) =>
                    {
                        Console.WriteLine("Replacing method " + m.Name);
                        MethodDefinition replacementMethod = GetMethodReplacement(m);

                        var targetType = m.DeclaringType;
                        var m1 = replacementMethod;
                        var m1IL = m1.Body.GetILProcessor();

                        foreach(var i in m1.Body.Instructions.ToList()){
                            var ci = i;
                            if(i.Operand is MethodReference){
                                var mref = i.Operand as MethodReference;
                                ci = m1IL.Create(i.OpCode, targetType.Module.Import(mref));
                            }
                            else if(i.Operand is TypeReference){
                                var tref = i.Operand as TypeReference;
                                ci = m1IL.Create(i.OpCode, targetType.Module.Import(tref.Resolve()));
                            }

                            if(ci != i){
                                m1IL.Replace(i, ci);
                            }
                        }

                        //here the source Body should have its Instructions set imported fine
                        //so we just need to set its Body to the target's Body
                        m.Body = m1.Body;

                        methodReplaced[m] = true;
                    }, value.BumpReplaceMethods));

        private void DoFixupEvents(AltererResult value)
        {
            bool FilterEvent(EventDefinition e)
            {
                // Sometimes, for some reason, events have the same name as their backing fields.
                // If both are public, neither can be accessed (name conflict).
                var backing = e.DeclaringType.Fields.SingleOrDefault(f => f.Name == e.Name);
                if (backing != null)
                {
                    backing.IsPrivate = true;
                    value.Fields--;
                }

                return e.AddMethod.IsPrivate || e.RemoveMethod.IsPrivate || (e.InvokeMethod?.IsPrivate ?? false);
            }

            void ProcessEvent(EventDefinition e)
            {
                e.AddMethod.IsPublic = true;
                e.RemoveMethod.IsPublic = true;

                if (e.InvokeMethod != null)
                    e.InvokeMethod.IsPublic = true;
            }

            ArrayProcessor<TypeDefinition, EventDefinition>(GetTypes(),
                (t) => t.Events,
                (es) => Processor<EventDefinition>(es,
                    FilterEvent,
                    ProcessEvent,
                    value.BumpEvents));
        }

        #endregion

        public void Dispose() => _assembly.Dispose();
    }
}


