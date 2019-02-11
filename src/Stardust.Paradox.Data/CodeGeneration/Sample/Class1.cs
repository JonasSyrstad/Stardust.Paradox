using Newtonsoft.Json;
using Stardust.Paradox.Data.Annotations;
using Stardust.Paradox.Data.Internals;
using System;
using System.Collections.Generic;
using System.Text;
#pragma warning disable 649

namespace Stardust.Paradox.Data.CodeGeneration.Sample
{
    public class Class1 : GraphDataEntity, IClass1
    {

        private string _name;
        private long _age;
        private string _email;
        private bool _validatedEmail;
        private readonly string _test;

        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Include)]
        public string Id
        {
            get { return _entityKey; }
            set { _entityKey = value; }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                if (OnPropertyChanging(value, _name, "name"))
                {
                    _name = value;
                    OnPropertyChanged(value, "name");
                }
            }
        }


        public string Test
        {
            get { return _test; }
            set { }
        }
        public long Age
        {
            get { return _age; }
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
            get { return _email; }
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
            get { return _validatedEmail; }
            set
            {
                if (_validatedEmail != value)
                {
                    _validatedEmail = value;
                    OnPropertyChanged(value, "validatedEmail");
                }
            }
        }

        public IEdgeCollection<IClass1> Parent
        {
            get { return GetEdgeCollection<IClass1>("parent", "t", ""); }
        }

        public IInlineCollection<string> Inline
        {
            get { return GetInlineCollection<string>("Inline"); }
        }

        public override string Label => "s";
    }
}
