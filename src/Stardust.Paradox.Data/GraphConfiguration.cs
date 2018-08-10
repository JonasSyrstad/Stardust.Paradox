using System;
using System.Reflection;
using Stardust.Paradox.Data.CodeGeneration;
using Stardust.Paradox.Data.Internals;

namespace Stardust.Paradox.Data
{
    internal class GraphConfiguration : IGraphConfiguration
    {
        private readonly GraphContextBase _context;

        public GraphConfiguration(GraphContextBase context)
        {
            _context = context;
        }

        IGraphConfiguration IGraphConfiguration.AddCollection<T>()
        {
            var type = typeof(T);
            var label = type.GetCustomAttribute<VertexLabelAttribute>()?.Label ??
                        type.Name.Remove(0, 1).ToCamelCase();
            if (!GraphContextBase._dataSetLabelMapping.ContainsKey(type))
            {
                GenerateObservable(type, label);

                GraphContextBase._dataSetLabelMapping.Add(type, label);
            }
            return this;
        }

        IGraphConfiguration IGraphConfiguration.AddCollection<T>(string label)
        {
            var type = typeof(T);
            if (!GraphContextBase._dataSetLabelMapping.ContainsKey(type))
            {
                GenerateObservable(type, label);
                GraphContextBase._dataSetLabelMapping.Add(type, label);
            }
            return this;
        }

        private void GenerateObservable(Type type, string label)
        {
            var implementation = CodeGenerator.MakeDataEntity(type, label);
            DependencyResolverAdapter.AddEntity(type, implementation);
            //Resolver.GetConfigurator().Bind(type).To(implementation).SetTransientScope();
            GraphJsonConverter.AddPair(type, implementation);
        }
    }
}