using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;
using Stardust.Particles;
using Stardust.Particles.Collection;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Stardust.Paradox.Data.CodeGeneration
{
    public class CodeGenerator
    {
        internal static Dictionary<Type, Dictionary<MemberInfo, FluentConfig>> _FluentConfig = new Dictionary<Type, Dictionary<MemberInfo, FluentConfig>>();

        private static AssemblyBuilder _builder;
        private static ModuleBuilder _moduleBuilder;

        internal static string EdgeLabel(Type entityType, MemberInfo member)
        {
            var def = GetMemberBinding(entityType, member);
            return def?.EdgeLabel;
        }

        private static EagerAttribute EagerLoading(Type entityType, MemberInfo member)
        {
            var def = GetMemberBinding(entityType, member);
            if (def != null && !def.EagerLoading) return null;
            return new EagerAttribute();
        }

        internal static string EdgeReverseLabel(Type entityType, MemberInfo member)
        {
            var def = GetMemberBinding(entityType, member);
            return def?.ReverseEdgeLabel;
        }

        private static string InlineQuery(Type entityType, MemberInfo member)
        {
            var def = GetMemberBinding(entityType, member);
            return def?.Query;
        }

        private static InlineSerializationAttribute Serialization(Type entityType, MemberInfo member)
        {
            var def = GetMemberBinding(entityType, member);
            if (def?.Serialization == null) return null;
            return new InlineSerializationAttribute(def.Serialization.Value);
        }

        private static FluentConfig GetMemberBinding(Type entityType, MemberInfo member)
        {
            FluentConfig def = null;
            if (_FluentConfig.TryGetValue(entityType, out Dictionary<MemberInfo, FluentConfig> t))
            {
                t.TryGetValue(member, out def);
            }

            return def;
        }
		internal static ConcurrentDictionary<Type,string> EdgeLables=new ConcurrentDictionary<Type, string>();
        public static Type MakeEdgeDataEntity(Type entity, string label)
        {
	        EdgeLables.TryAdd(entity, label);
            var dataContract = entity;
            var generics = entity.GetInterfaces().Single(c => c.GenericTypeArguments.Length == 2).GenericTypeArguments;
            var baseType = typeof(EdgeDataEntity<,>).MakeGenericType(generics);
            if (_builder == null)
                _builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Data.Contracts.Implementations"),
                    AssemblyBuilderAccess.Run);
            if (_moduleBuilder == null)
                _moduleBuilder = _builder.DefineDynamicModule("Data.Contracts.Implementations");
            var typeBuilder = _moduleBuilder.DefineType("Data.Contracts.Implementations.Edges" + entity.Name.Remove(0, 1),
                TypeAttributes.Public | TypeAttributes.Class,
               baseType,
                new[] { dataContract }
            );
            AddLabelProperty(label, typeBuilder);
            AddIdProperty(typeBuilder,baseType);
            var eagerProperties = new List<string>();
            foreach (var prop in dataContract.GetProperties())
            {
                var eager = EagerLoading(entity, prop) ?? prop.GetCustomAttribute<EagerAttribute>();
                var serialization = Serialization(entity, prop) ?? prop.GetCustomAttribute<InlineSerializationAttribute>();
                if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType.IsGenericType)
                {
                    if (serialization != null)
                    {
                        AddInline(prop, typeBuilder, serialization, entity,baseType);
                        InlineCollection<string>.SetSerializationType($"{typeBuilder.FullName}.{prop.Name}", serialization?.Type ?? SerializationType.ClearText);

                    }
                    //else
                    //{
                    //    AddEdge(entity, prop, typeBuilder);
                    //    if (eager != null)
                    //        eagerProperties.Add(prop.Name);
                    //}
                }
                //else if (typeof(IEdgeReference).IsAssignableFrom(prop.PropertyType))
                //{
                //    AddEdgeRef(prop, typeBuilder, entity);
                //    if (eager != null)
                //        eagerProperties.Add(prop.Name);
                //}
                else if (prop.SetMethod != null)
                    AddValueProperty(typeBuilder, prop,baseType);
            }
            if (eagerProperties.ContainsElements())
                GraphDataEntity._eagerLodedProperties.TryAdd(typeBuilder.FullName, eagerProperties);
            return typeBuilder.CreateTypeInfo();
        }


        public static Type MakeDataEntity(Type entity, string label)
        {
            var baseType = typeof(GraphDataEntity);
            var dataContract = entity;
            if (_builder == null)
                _builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Data.Contracts.Implementations"),
                    AssemblyBuilderAccess.Run);
            if (_moduleBuilder == null)
                _moduleBuilder = _builder.DefineDynamicModule("Data.Contracts.Implementations");
            var typeBuilder = _moduleBuilder.DefineType("Data.Contracts.Implementations.Vertices" + entity.Name.Remove(0, 1),
                TypeAttributes.Public | TypeAttributes.Class,
                baseType,
                new[] { dataContract }
            );
            AddLabelProperty(label, typeBuilder);
            AddIdProperty(typeBuilder, baseType);
            var eagerProperties = new List<string>();
            foreach (var prop in dataContract.GetProperties())
            {
                var eager = EagerLoading(entity, prop) ?? prop.GetCustomAttribute<EagerAttribute>();
                var serialization = Serialization(entity, prop) ?? prop.GetCustomAttribute<InlineSerializationAttribute>();
                if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType.IsGenericType)
                {
                    if (serialization != null)
                    {
                        AddInline(prop, typeBuilder, serialization, entity,baseType);
                        InlineCollection<string>.SetSerializationType($"{typeBuilder.FullName}.{prop.Name}", serialization?.Type ?? SerializationType.ClearText);

                    }
                    else
                    {
                        AddEdge(entity, prop, typeBuilder,baseType);
                        if (eager != null)
                            eagerProperties.Add(prop.Name);
                    }
                }
                else if (typeof(IEdgeReference).IsAssignableFrom(prop.PropertyType))
                {
                    AddEdgeRef(prop, typeBuilder, entity,baseType);
                    if (eager != null)
                        eagerProperties.Add(prop.Name);
                }
                else if (prop.SetMethod != null)
                    AddValueProperty(typeBuilder, prop,baseType);
            }
            if (eagerProperties.ContainsElements())
                GraphDataEntity._eagerLodedProperties.TryAdd(typeBuilder.FullName, eagerProperties);
            return typeBuilder.CreateTypeInfo();
        }

        private static void AddValueProperty(TypeBuilder typeBuilder, PropertyInfo prop, Type baseType)
        {
            var field = BuildField(typeBuilder, prop);
            var property = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, CallingConventions.Standard,
                prop.PropertyType, null);
            var get = BuildMethodget(typeBuilder, prop, field);
            var set = BuildMethodset(typeBuilder, prop, field,baseType);
            property.SetGetMethod(get);
            property.SetSetMethod(set);
            property.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(JsonPropertyAttribute).GetConstructor(new[] { typeof(string) }), new[] { prop.Name.ToCamelCase() },
                typeof(JsonPropertyAttribute).GetProperties().Where(p => p.Name == "DefaultValueHandling").ToArray(),
                new object[] { DefaultValueHandling.Include }));
        }

        private static void AddEdge(Type entity, PropertyInfo prop, TypeBuilder typeBuilder, Type baseType)
        {
            var edgeLabel = EdgeLabel(entity, prop) ?? prop.GetCustomAttribute<EdgeLabelAttribute>()?.Label;
            var reverseEdgeLabel = EdgeReverseLabel(entity, prop) ?? prop.GetCustomAttribute<ReverseEdgeLabelAttribute>()?.ReverseLabel;
            var gremlinQuery = InlineQuery(entity, prop) ?? prop.GetCustomAttribute<GremlinQueryAttribute>()?.Query;
            if (gremlinQuery.ContainsCharacters()) edgeLabel = prop.Name;
            var towayEdgeLabel = prop.GetCustomAttribute<ToWayEdgeLabelAttribute>()?.Label;
            if (towayEdgeLabel != null)
                reverseEdgeLabel = edgeLabel = towayEdgeLabel;
            var edgePropGet = BuildMethodget_Parent(typeBuilder, prop, edgeLabel ?? "", reverseEdgeLabel ?? "", gremlinQuery ?? "");
            var edgeProp = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, CallingConventions.Standard,
                prop.PropertyType, null);
            edgeProp.SetGetMethod(edgePropGet);
            edgeProp.SetSetMethod(BuildMethodset(typeBuilder, prop,baseType));
            edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(JsonIgnoreAttribute).GetConstructor(new Type[] { }),
                new object[] { }));
            if (reverseEdgeLabel != null)
                edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(ReverseEdgeLabelAttribute).GetConstructor(new Type[] { typeof(string) }),
                    new object[] { reverseEdgeLabel }));
            if (edgeLabel != null)
                edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(EdgeLabelAttribute).GetConstructor(new Type[] { typeof(string) }),
                    new object[] { edgeLabel }));
        }

        private static void AddInline(PropertyInfo prop, TypeBuilder typeBuilder, InlineSerializationAttribute serialization, Type entity, Type baseType)
        {
            var edgeLabel = EdgeLabel(entity, prop) ?? prop.GetCustomAttribute<EdgeLabelAttribute>()?.Label;
            var reverseEdgeLabel = EdgeReverseLabel(entity, prop) ?? prop.GetCustomAttribute<ReverseEdgeLabelAttribute>()?.ReverseLabel;
            var gremlinQuery = InlineQuery(entity, prop) ?? prop.GetCustomAttribute<GremlinQueryAttribute>()?.Query;
            var towayEdgeLabel = prop.GetCustomAttribute<ToWayEdgeLabelAttribute>()?.Label;
            if (towayEdgeLabel != null)
                reverseEdgeLabel = edgeLabel = towayEdgeLabel;
            var edgePropGet = BuildMethodget_Inline(typeBuilder, prop, edgeLabel ?? "", reverseEdgeLabel ?? "", gremlinQuery ?? "",baseType);
            var edgeProp = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, CallingConventions.Standard,
                prop.PropertyType, null);
            edgeProp.SetGetMethod(edgePropGet);
            edgeProp.SetSetMethod(BuildMethodset(typeBuilder, prop,baseType));
            edgeProp.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(JsonPropertyAttribute).GetConstructor(new[] { typeof(string) }), new[] { prop.Name.ToCamelCase() },
                typeof(JsonPropertyAttribute).GetProperties().Where(p => p.Name == "DefaultValueHandling").ToArray(),
                new object[] { DefaultValueHandling.Include }));
            edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(InlineSerializationAttribute).GetConstructor(new Type[] { typeof(SerializationType) }),
                new object[] { serialization.Type }));
        }

        private static void AddEdgeRef(PropertyInfo prop, TypeBuilder typeBuilder, Type entity, Type baseType)
        {
            var edgeLabel = EdgeLabel(entity, prop) ?? prop.GetCustomAttribute<EdgeLabelAttribute>()?.Label;
            var reverseEdgeLabel = EdgeReverseLabel(entity, prop) ?? prop.GetCustomAttribute<ReverseEdgeLabelAttribute>()?.ReverseLabel;
            var towayEdgeLabel = prop.GetCustomAttribute<ToWayEdgeLabelAttribute>()?.Label;
            var gremlinQuery = InlineQuery(entity, prop) ?? prop.GetCustomAttribute<GremlinQueryAttribute>()?.Query;
            if (gremlinQuery.ContainsCharacters()) edgeLabel = prop.Name;
            if (towayEdgeLabel != null)
                reverseEdgeLabel = edgeLabel = towayEdgeLabel;
            var edgePropGet = BuildMethodget_Ref(typeBuilder, prop, edgeLabel ?? "", reverseEdgeLabel ?? "", gremlinQuery ?? "");
            var edgeProp = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, CallingConventions.Standard,
                prop.PropertyType, null);
            edgeProp.SetGetMethod(edgePropGet);
            edgeProp.SetSetMethod(BuildMethodset(typeBuilder, prop,baseType));
            edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(JsonIgnoreAttribute).GetConstructor(new Type[] { }),
                new object[] { }));
            if (reverseEdgeLabel != null)
                edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(ReverseEdgeLabelAttribute).GetConstructor(new Type[] { typeof(string) }),
                    new object[] { reverseEdgeLabel }));
            if (edgeLabel != null)
                edgeProp.SetCustomAttribute(new CustomAttributeBuilder(typeof(EdgeLabelAttribute).GetConstructor(new Type[] { typeof(string) }),
                    new object[] { edgeLabel }));
        }

        private static void AddIdProperty(TypeBuilder typeBuilder,Type baseType)
        {
            var idget = BuildMethodget_Id(typeBuilder,baseType);
            MethodBuilder idSet = BuildMethodset_Id(typeBuilder,baseType);
            var idProperty = typeBuilder.DefineProperty("Id", PropertyAttributes.None, CallingConventions.Standard,
                typeof(string), null);
            idProperty.SetGetMethod(idget);
            idProperty.SetSetMethod(idSet);
            idProperty.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(JsonPropertyAttribute).GetConstructor(new[] { typeof(string) }), new[] { "id" },
                typeof(JsonPropertyAttribute).GetProperties().Where(p => p.Name == "DefaultValueHandling").ToArray(),
                new object[] { DefaultValueHandling.Include }));
        }

        private static MethodBuilder BuildMethodset_Id(TypeBuilder typeBuilder, Type baseType)
        {
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.HideBySig;
            MethodBuilder method = typeBuilder.DefineMethod("set_Id", methodAttributes);
            // Preparing Reflection instances
            FieldInfo field1 = baseType.GetField("_entityKey", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
            // Setting return type
            method.SetReturnType(typeof(void));
            // Adding parameters
            method.SetParameters(
                typeof(String)
            );
            // Parameter value
            ParameterBuilder value = method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator gen = method.GetILGenerator();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field1);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        private static MethodBuilder BuildMethodset(TypeBuilder typeBuilder, PropertyInfo prop, Type baseType)
        {
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = typeBuilder.DefineMethod("set_" + prop.Name, methodAttributes);
            // Preparing Reflection instances
            //FieldInfo field1 = typeof(GraphDataEntity).GetField("_entityKey", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
            // Setting return type
            method.SetReturnType(typeof(void));
            // Adding parameters
            method.SetParameters(
                prop.PropertyType
            );
            // Parameter value
            ILGenerator gen = method.GetILGenerator();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        private static void AddLabelProperty(string label, TypeBuilder typeBuilder)
        {
            var labelGet = BuildMethodget_Label(typeBuilder, label);
            var labelProperty = typeBuilder.DefineProperty("Label", PropertyAttributes.None, CallingConventions.Standard,
                typeof(string), null);
            labelProperty.SetGetMethod(labelGet);
            labelProperty.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(JsonPropertyAttribute).GetConstructor(new[] { typeof(string) }), new[] { "label" },
                typeof(JsonPropertyAttribute).GetProperties().Where(p => p.Name == "DefaultValueHandling").ToArray(),
                new object[] { DefaultValueHandling.Include }));
        }


        private static MethodBuilder BuildMethodget(TypeBuilder type, PropertyInfo i, FieldBuilder field1)
        {
            // Declaring method builder
            // Method attributes
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = type.DefineMethod($"get_{i.Name}", methodAttributes);
            // Preparing Reflection instances
            //FieldInfo field1 = type.GetField($"_{i.Name}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
            // Setting return type
            method.SetReturnType(i.PropertyType);
            // Adding parameters
            ILGenerator gen = method.GetILGenerator();
            // Preparing locals
            LocalBuilder str = gen.DeclareLocal(i.PropertyType);
            // Preparing labels
            Label label10 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field1);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label10);
            gen.MarkLabel(label10);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        private static FieldBuilder BuildField(TypeBuilder type, PropertyInfo i)
        {
            FieldBuilder field = type.DefineField(
                "_" + i.Name,
                i.PropertyType,
                FieldAttributes.Private
            );
            return field;
        }

        private static MethodBuilder BuildMethodget_Id(TypeBuilder type, Type baseType)
        {
            // Declaring method builder
            // Method attributes
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = type.DefineMethod("get_Id", methodAttributes);
            // Preparing Reflection instances
            FieldInfo field1 = baseType.GetField("_entityKey", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
            // Setting return type
            method.SetReturnType(typeof(String));
            // Adding parameters
            ILGenerator gen = method.GetILGenerator();
            // Preparing locals
            LocalBuilder str = gen.DeclareLocal(typeof(String));
            // Preparing labels
            Label label10 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field1);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label10);
            gen.MarkLabel(label10);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }


        private static MethodBuilder BuildMethodset(TypeBuilder type, PropertyInfo i, FieldBuilder field1,Type baseType)
        {
            // Method attributes
            var methodAttributes =
                  MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = type.DefineMethod($"set_{i.Name}", methodAttributes);
            // Preparing Reflection instances

            MethodInfo method2 = baseType.GetMethod(
                "OnPropertyChanging",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[]{
            typeof(Object),
            typeof(Object),
            typeof(String)
                    },
                null
                );
            MethodInfo method3 = baseType.GetMethod(
                "OnPropertyChanged",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[]{
            typeof(Object),
            typeof(String)
                    },
                null
                );
            // Setting return type
            method.SetReturnType(typeof(void));
            // Adding parameters
            method.SetParameters(
                i.PropertyType
                );
            // Parameter value
            ParameterBuilder value = method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator gen = method.GetILGenerator();
            // Preparing locals
            LocalBuilder flag = gen.DeclareLocal(typeof(Boolean));
            // Preparing labels
            Label label45 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            if (!i.PropertyType.IsClass)
                gen.Emit(OpCodes.Box, i.PropertyType);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field1);
            if (!i.PropertyType.IsClass)
                gen.Emit(OpCodes.Box, i.PropertyType);
            gen.Emit(OpCodes.Ldstr, i.Name);
            gen.Emit(OpCodes.Call, method2);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Brfalse_S, label45);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field1);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            if (!i.PropertyType.IsClass)
                gen.Emit(OpCodes.Box, i.PropertyType);
            gen.Emit(OpCodes.Ldstr, i.Name);
            gen.Emit(OpCodes.Callvirt, method3);
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Nop);
            gen.MarkLabel(label45);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;


            //// Declaring method builder
            //// Method attributes
            //MethodAttributes methodAttributes =
            //      MethodAttributes.Public
            //    | MethodAttributes.Virtual
            //    | MethodAttributes.Final
            //    | MethodAttributes.HideBySig
            //    | MethodAttributes.NewSlot;
            //MethodBuilder method = type.DefineMethod("set_" + i.Name, methodAttributes);
            //// Preparing Reflection instances
            ////FieldInfo field1 = typeof(Class1).GetField("_age", BindingFlags.Public | BindingFlags.NonPublic);
            //MethodInfo method2 = typeof(GraphDataEntity).GetMethod(
            //    "OnPropertyChanged",
            //    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            //    null,
            //    new Type[]{
            //typeof(Object),
            //typeof(String)
            //        },
            //    null
            //    );
            //// Setting return type
            //method.SetReturnType(typeof(void));
            //// Adding parameters
            //method.SetParameters(
            //    i.PropertyType
            //    );
            //// Parameter value
            //ParameterBuilder value = method.DefineParameter(1, ParameterAttributes.None, "value");
            //ILGenerator gen = method.GetILGenerator();
            //// Preparing locals
            //LocalBuilder flag = gen.DeclareLocal(typeof(Boolean));
            //// Preparing labels
            //Label label44 = gen.DefineLabel();
            //// Writing body
            //gen.Emit(OpCodes.Nop);
            //gen.Emit(OpCodes.Ldarg_0);
            //gen.Emit(OpCodes.Ldfld, field1);
            //gen.Emit(OpCodes.Ldarg_1);
            //gen.Emit(OpCodes.Ceq);
            //gen.Emit(OpCodes.Ldc_I4_0);
            //gen.Emit(OpCodes.Ceq);
            //gen.Emit(OpCodes.Stloc_0);
            //gen.Emit(OpCodes.Ldloc_0);
            //gen.Emit(OpCodes.Brfalse_S, label44);
            //gen.Emit(OpCodes.Nop);
            //gen.Emit(OpCodes.Ldarg_0);
            //gen.Emit(OpCodes.Ldarg_1);
            //gen.Emit(OpCodes.Stfld, field1);
            //gen.Emit(OpCodes.Ldarg_0);
            //gen.Emit(OpCodes.Ldarg_1);
            //gen.Emit(OpCodes.Box, i.PropertyType);
            //gen.Emit(OpCodes.Ldstr, i.Name);
            //gen.Emit(OpCodes.Callvirt, method2);
            //gen.Emit(OpCodes.Nop);
            //gen.Emit(OpCodes.Nop);
            //gen.MarkLabel(label44);
            //gen.Emit(OpCodes.Ret);
            //// finished
            //return method;

        }

        private static MethodBuilder BuildMethodget_Parent(TypeBuilder type, PropertyInfo prop, string label, string reverseLabel, string gremlinQuery)
        {
            // Declaring method builder
            // Method attributes
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = type.DefineMethod("get_" + prop.Name, methodAttributes);
            // Preparing Reflection instances
            MethodInfo method1 = typeof(GraphDataEntity).GetMethod(
                "GetEdgeCollection",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[]{
                    typeof(String),
                    typeof(String),
                    typeof(String)
                },
                null
            );
            method1 = method1.MakeGenericMethod(prop.PropertyType.GenericTypeArguments);
            // Setting return type
            method.SetReturnType(prop.PropertyType);
            // Adding parameters
            ILGenerator gen = method.GetILGenerator();
            // Preparing locals
            LocalBuilder edges = gen.DeclareLocal(prop.PropertyType);
            // Preparing labels
            Label label15 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, label);
            gen.Emit(OpCodes.Ldstr, reverseLabel);
            gen.Emit(OpCodes.Ldstr, gremlinQuery);
            gen.Emit(OpCodes.Call, method1);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label15);
            gen.MarkLabel(label15);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        private static MethodBuilder BuildMethodget_Inline(TypeBuilder type, PropertyInfo prop, string label,
            string reverseLabel, string gremlinQuery, Type baseType)
        {
            // Declaring method builder
            // Method attributes
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = type.DefineMethod("get_" + prop.Name, methodAttributes);
            // Preparing Reflection instances
            MethodInfo method1 =baseType.GetMethod(
                "GetInlineCollection",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[]{
                    typeof(String)
                },
                null
            );
            method1 = method1.MakeGenericMethod(prop.PropertyType.GenericTypeArguments);
            // Setting return type
            method.SetReturnType(prop.PropertyType);
            // Adding parameters
            ILGenerator gen = method.GetILGenerator();
            // Preparing locals
            LocalBuilder edges = gen.DeclareLocal(prop.PropertyType);
            // Preparing labels
            Label label15 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, prop.Name);
            gen.Emit(OpCodes.Call, method1);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label15);
            gen.MarkLabel(label15);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        private static MethodBuilder BuildMethodget_Ref(TypeBuilder type, PropertyInfo prop, string label, string reverseLabel, string gremlinQuery)
        {
            // Declaring method builder
            // Method attributes
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;
            MethodBuilder method = type.DefineMethod("get_" + prop.Name, methodAttributes);
            // Preparing Reflection instances
            MethodInfo method1 = typeof(GraphDataEntity).GetMethod(
                "GetEdgeReference",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[]{
                    typeof(String),
                    typeof(String),
                    typeof(String)
                },
                null
            );
            method1 = method1.MakeGenericMethod(prop.PropertyType.GenericTypeArguments);
            // Setting return type
            method.SetReturnType(prop.PropertyType);
            // Adding parameters
            ILGenerator gen = method.GetILGenerator();
            // Preparing locals
            LocalBuilder edges = gen.DeclareLocal(prop.PropertyType);
            // Preparing labels
            Label label15 = gen.DefineLabel();
            // Writing body
            gen.Emit(OpCodes.Nop);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldstr, label);
            gen.Emit(OpCodes.Ldstr, reverseLabel);
            gen.Emit(OpCodes.Ldstr, gremlinQuery);
            gen.Emit(OpCodes.Call, method1);
            gen.Emit(OpCodes.Stloc_0);
            gen.Emit(OpCodes.Br_S, label15);
            gen.MarkLabel(label15);
            gen.Emit(OpCodes.Ldloc_0);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }

        private static MethodBuilder BuildMethodget_Label(TypeBuilder type, string label)
        {
            // Declaring method builder
            // Method attributes
            MethodAttributes methodAttributes =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.HideBySig;
            MethodBuilder method = type.DefineMethod("get_Label", methodAttributes);
            // Preparing Reflection instances
            // Setting return type
            method.SetReturnType(typeof(string));
            // Adding parameters
            ILGenerator gen = method.GetILGenerator();
            // Writing body
            gen.Emit(OpCodes.Ldstr, label);
            gen.Emit(OpCodes.Ret);
            // finished
            return method;
        }
    }
}
