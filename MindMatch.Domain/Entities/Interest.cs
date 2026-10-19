using MindMatch.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Domain.Entities
{
  
    public class Interest : BaseEntity
    {
        public string Name { get; private set; } = string.Empty;
        public string NormalizedName { get; private set; } = string.Empty;

        private Interest() { }

        public Interest(string name)
        {
            SetName(name);
        }

        public void SetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("İlgi alanı adı boş olamaz.");

            name = name.Trim();

            if (name.Length > 50)
                throw new ArgumentException("İlgi alanı en fazla 50 karakter olabilir.");

            Name = name;
            NormalizedName = name.ToUpperInvariant();
        }
    }
}
