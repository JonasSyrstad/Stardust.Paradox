using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;

#pragma warning disable 649

namespace Stardust.Paradox.Data.CodeGeneration.Sample
{
    public class Class1 : GraphDataEntity, IClass1
    {
        private readonly string _test;
        private long _age;
        private string _email;

        private string _name;
        private bool _validatedEmail;

        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Include)]
        public string Id
        {
            get => _entityKey;
            set => _entityKey = value;
        }


        public string Test
        {
            get => _test;
            set { }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (OnPropertyChanging(value, _name, "name"))
                {
                    _name = value;
                    OnPropertyChanged(value, "name");
                }
            }
        }

        public long Age
        {
            get => _age;
            set
            {
                if (OnPropertyChanging(value, _age, "name"))
                {
                    _age = value;
                    OnPropertyChanged(value, "age");
                }
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                if (_email != value)
                {
                    _email = value;
                    OnPropertyChanged(value, "email");
                }
            }
        }

        public bool ValidatedEmail
        {
            get => _validatedEmail;
            set
            {
                if (_validatedEmail != value)
                {
                    _validatedEmail = value;
                    OnPropertyChanged(value, "validatedEmail");
                }
            }
        }

        public IEdgeCollection<IClass1> Parent => GetEdgeCollection<IClass1>("parent", "t", "");

        public IInlineCollection<string> Inline => GetInlineCollection<string>("Inline");

        public override string Label => "s";
    }
}