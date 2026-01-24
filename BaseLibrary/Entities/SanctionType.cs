using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BaseLibrary.Entities
{
    public class SanctionType : BaseEntity
    {
        //Many to one relationship with Vacation
        [JsonIgnore]
        public List<Sanction>? Sanctions { get; set; }
    }
}
