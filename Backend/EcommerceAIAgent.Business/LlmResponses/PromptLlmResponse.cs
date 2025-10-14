using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Business.LlmResponses
{
    public class PromptLlmResponse
    {
        // U zavisnosti koliko je korisnik trazio proizvoda u jednom izdvojenom promptu toliki ce biti i MaxResults
        // Maksimalan broj koji MaxResults moze da uzima je 10, ako korisnik trazi 15, LLM treba uz top 10 proizvoda da mu kaze da mozemo da mu prikazemo samo top 10 
        // ## Primeri
        // *pr. 1:* Ako je korisnik pitao opste pitanje o jednom proizvodu ovo polje bi trebalo da ima vrednost 1, nakon toga bi trebali da pretrazimo vektorsku bazu podataka samo za jedan najslicniji proizvod i iz njega probamo da odgovorimo na korisnikovo pitanje
        // *pr. 2:* Ako je korisnik pitao: Daj mi top {MaxResults} proizvoda
        public int? MaxResults { get; set; }

        // Ako korisnik trazi cenu oko neke cifre PriceLowerLimit i PriceUpperLimit postavi na vrednosti srazmerne ceni kojoj je korisnik uneo (npr. manja odstupanja ces da koristis ako je proizvod generalno jeftin)
        // ## Primeri
        // *pr. 1:* Daj mi {MaxResults} proizvoda oko 10000 dinara => PriceLowerLimit=9000, PriceUpperLimit=11000
        // *pr. 2:* Ako je korisnik pitao: Daj mi top {MaxResults} proizvoda
        public int? PriceLowerLimit { get; set; }
        public int? PriceUpperLimit { get; set; }

        // Ako korisnik npr. trazi tacnu cenu proizvoda, mi iz vektorske baze to ne mozemo da znamo jer su tamo okvirne cene, moramo da iz real time apija povucemo podatke i tako damo korisniku tacan odgovor.
        public List<int> ProductIds { get; set; }
    }
}
