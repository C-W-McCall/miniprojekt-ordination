namespace ordination_test;

using Microsoft.EntityFrameworkCore;

using Service;
using Data;
using shared.Model;

// DagligFast, DagligSkæv og PN test


[TestClass]
public class ServiceTest
{
    private DataService service;

    [TestInitialize]
    public void SetupBeforeEachTest()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrdinationContext>();
        optionsBuilder.UseInMemoryDatabase(databaseName: "test-database");
        var context = new OrdinationContext(optionsBuilder.Options);
        service = new DataService(context);
        service.SeedData();
    }

    [TestMethod]
    public void PatientsExist()
    {
        Assert.IsNotNull(service.GetPatienter());
    }

    [TestMethod]
    public void OpretDagligFast()
    {
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();

        Assert.AreEqual(1, service.GetDagligFaste().Count());

        service.OpretDagligFast(patient.PatientId, lm.LaegemiddelId,
            2, 2, 1, 0, DateTime.Now, DateTime.Now.AddDays(3));

        Assert.AreEqual(2, service.GetDagligFaste().Count());
    }

    [TestMethod]
    public void OpretDagligSkaev()
    {
        // henter patient og lægemiddel
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        Assert.AreEqual(1, service.GetDagligSkæve().Count()); // tjekker der er 1 daglig skæv i seeddata
        
        // opretter nye doser
        Dosis[] doser = new Dosis[] {
            new Dosis(DateTime.Now.Date.AddHours(8), 2),
            new Dosis(DateTime.Now.Date.AddHours(12), 1),
            new Dosis(DateTime.Now.Date.AddHours(18), 2)
        };
        
        // kalder dagligskæv metoden og opretter en ny daglig skæv med doserne
        service.OpretDagligSkaev(patient.PatientId, lm.LaegemiddelId, 
            doser, DateTime.Now, DateTime.Now.AddDays(3));
        
        // tjekker der nu er 2 daglig skæv i seeddata
        Assert.AreEqual(2, service.GetDagligSkæve().Count());
    }

    [TestMethod]
    public void OpretPN()
    {
        // henter patient og lægemiddel
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        Assert.AreEqual(4, service.GetPNs().Count()); // tjekker der er 4 PN i seeddata
        
        // kalder OpretPN metoden og opretter en ny PN ordination
        service.OpretPN(patient.PatientId, lm.LaegemiddelId, 
            5, DateTime.Now, DateTime.Now.AddDays(7));
        
        // tjekker der nu er 5 PN i seeddata
        Assert.AreEqual(5, service.GetPNs().Count());
    }

    [TestMethod]
    public void AnvendOrdination()
    {
        // henter patient og lægemiddel
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        // opretter en ny PN ordination
        PN pn = service.OpretPN(patient.PatientId, lm.LaegemiddelId, 
            5, DateTime.Now, DateTime.Now.AddDays(7));
        
        // tjekker at der ikke er nogen datoer registreret endnu
        Assert.AreEqual(0, pn.getAntalGangeGivet());
        
        // anvender ordinationen på en gyldig dato
        Dato dato = new Dato { dato = DateTime.Now.AddDays(2) };
        string resultat = service.AnvendOrdination(pn.OrdinationId, dato);
        
        // tjekker at resultatet er korrekt
        Assert.AreEqual("Dosis givet", resultat);
        
        // henter ordinationen igen for at se opdateringen
        PN opdateretPN = service.GetPNs().First(p => p.OrdinationId == pn.OrdinationId);
        
        // tjekker at datoen blev registreret
        Assert.AreEqual(1, opdateretPN.getAntalGangeGivet());
    }

    [TestMethod]
    public void BeregnAnbefaletDosis()
    {
        // henter patient og lægemiddel
        Patient patient = service.GetPatienter().First(); // Jane Jensen, 63.4 kg
        Laegemiddel lm = service.GetLaegemidler().First(l => l.navn == "Paracetamol"); // enhedPrKgPrDoegnNormal = 1.5
        
        // beregner anbefalet dosis
        double anbefaletDosis = service.GetAnbefaletDosisPerDøgn(patient.PatientId, lm.LaegemiddelId);
        
        // forventet resultat: 63.4 kg * 1.5 = 95.1
        double forventet = patient.vaegt * lm.enhedPrKgPrDoegnNormal;
        
        // tjekker at den beregnede dosis er korrekt
        Assert.AreEqual(forventet, anbefaletDosis, 0.001);
    }

    [TestMethod]
    public void TestDagligFastSamletDosis()
    {
        // Opretter en DagligFast ordination med kendte værdier
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        // Opretter ordination: 2 om morgenen, 1 ved middag, 1 om aftenen, 0 om natten
        // Fra i dag til om 3 dage = 4 dage total
        DagligFast df = service.OpretDagligFast(patient.PatientId, lm.LaegemiddelId,
            2, 1, 1, 0, DateTime.Now, DateTime.Now.AddDays(3));
        
        // Døgndosis = 2 + 1 + 1 + 0 = 4 enheder pr. døgn
        // Antal dage = 4 dage (dag 0, 1, 2, 3)
        // Samlet dosis = 4 * 4 = 16 enheder
        Assert.AreEqual(4, df.doegnDosis());
        Assert.AreEqual(16, df.samletDosis());
    }

    [TestMethod]
    public void TestPNDoegnDosis()
    {
        // Opretter en PN ordination med 5 enheder pr. gang
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        PN pn = service.OpretPN(patient.PatientId, lm.LaegemiddelId,
            5, DateTime.Now, DateTime.Now.AddDays(10));
        
        // Anvender ordinationen 3 gange over 5 dage
        service.AnvendOrdination(pn.OrdinationId, new Dato { dato = DateTime.Now });
        service.AnvendOrdination(pn.OrdinationId, new Dato { dato = DateTime.Now.AddDays(2) });
        service.AnvendOrdination(pn.OrdinationId, new Dato { dato = DateTime.Now.AddDays(4) });
        
        // Henter opdateret PN
        PN opdateretPN = service.GetPNs().First(p => p.OrdinationId == pn.OrdinationId);
        
        // Døgndosis = (3 gange * 5 enheder) / 5 dage = 15 / 5 = 3 enheder pr. døgn
        Assert.AreEqual(3, opdateretPN.doegnDosis());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestOpretDagligFastMedUgyldigeDatoer()
    {
        // Forsøger at oprette en ordination hvor slutdato er før startdato
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        // Dette skal smide en ArgumentException
        service.OpretDagligFast(patient.PatientId, lm.LaegemiddelId,
            2, 1, 1, 0, DateTime.Now, DateTime.Now.AddDays(-5));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestAnvendOrdinationUdenforPeriode()
    {
        // Opretter en PN ordination udenfor gyldigshedsperiode
        Patient patient = service.GetPatienter().First();
        Laegemiddel lm = service.GetLaegemidler().First();
        
        PN pn = service.OpretPN(patient.PatientId, lm.LaegemiddelId,
            5, DateTime.Now, DateTime.Now.AddDays(5));
        
        // Dette skal smide en ArgumentException
        service.AnvendOrdination(pn.OrdinationId, new Dato { dato = DateTime.Now.AddDays(10) });
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TestAtKodenSmiderEnException()
    {
        // Kalder GetAnbefaletDosisPerDøgn med et ugyldigt patient ID
        // Dette skal smide en ArgumentNullException
        service.GetAnbefaletDosisPerDøgn(999999, 1);
    }
}