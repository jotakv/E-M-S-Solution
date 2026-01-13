using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseLibrary.Entities
{
    public class City : BaseEntity
    {
        //Many-to-one relationship with Country
        public List<Country>? Country { get; set; }
        public int CountryId { get; set; }

        //Many-to-one relationship with Town
        public List<Town>? Towns { get; set; }

    }
}
