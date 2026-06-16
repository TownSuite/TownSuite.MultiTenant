namespace TownSuite.MultiTenant.Tests;

public class Tenant_Tests
{
    [Test]
    public void Clone_Test()
    {
        var t = new Tenant("abc");
        t.TryAddConnection("app1", "conn1");
        t.TryAddConnection("app2", "conn2");

        var clone = t.Clone() as Tenant;
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.UniqueId, Is.EqualTo(t.UniqueId));
        Assert.That(clone.Connections.Count, Is.EqualTo(t.Connections.Count));
        Assert.That(clone.Connections["app1"], Is.EqualTo(t.Connections["app1"]));
        Assert.That(clone.Connections["app2"], Is.EqualTo(t.Connections["app2"]));
    }

    [Test]
    public void Clone_Is_Independent_Of_Original()
    {
        var t = new Tenant("abc");
        t.TryAddConnection("app1", "conn1");
        t.TryAddAlias("abc");

        var clone = (Tenant)t.Clone();

        // Mutating the clone must not leak back into the original.
        clone.TryAddConnection("app2", "conn2");
        clone.TryAddAlias("xyz");

        Assert.That(t.Connections.Count, Is.EqualTo(1));
        Assert.That(t.Connections.ContainsKey("app2"), Is.False);
        Assert.That(t.Aliases, Does.Not.Contain("xyz"));

        Assert.That(clone.Connections.Count, Is.EqualTo(2));
        Assert.That(clone.Aliases, Does.Contain("xyz"));
    }

    [Test]
    public void Equals_Test()
    {
        var t1 = new Tenant("abc");
        t1.TryAddConnection("app1", "conn1");
        t1.TryAddConnection("app2", "conn2");

        var t2 = new Tenant("abc");
        t2.TryAddConnection("app1", "conn1");
        t2.TryAddConnection("app2", "conn2");

        Assert.That(t1.Equals(t2), Is.True);
    }

    [Test]
    public void Equals_False_When_UniqueId_Differs()
    {
        var t1 = new Tenant("abc");
        t1.TryAddConnection("app1", "conn1");

        var t2 = new Tenant("def");
        t2.TryAddConnection("app1", "conn1");

        Assert.That(t1.Equals(t2), Is.False);
    }

    [Test]
    public void Equals_False_When_Connection_Value_Differs()
    {
        var t1 = new Tenant("abc");
        t1.TryAddConnection("app1", "conn1");

        var t2 = new Tenant("abc");
        t2.TryAddConnection("app1", "DIFFERENT");

        Assert.That(t1.Equals(t2), Is.False);
    }

    [Test]
    public void Equals_False_When_Other_Has_Extra_Connection()
    {
        var t1 = new Tenant("abc");
        t1.TryAddConnection("app1", "conn1");

        var t2 = new Tenant("abc");
        t2.TryAddConnection("app1", "conn1");
        t2.TryAddConnection("app2", "conn2");

        Assert.That(t1.Equals(t2), Is.False);
        Assert.That(t2.Equals(t1), Is.False);
    }

    [Test]
    public void Equals_False_When_Other_Is_Null()
    {
        var t1 = new Tenant("abc");
        Assert.That(t1.Equals(null), Is.False);
    }

    [Test]
    public void TryAddConnection_Is_Idempotent_By_Name()
    {
        var t = new Tenant("abc");
        Assert.That(t.TryAddConnection("app1", "conn1"), Is.True);
        Assert.That(t.TryAddConnection("app1", "conn2"), Is.False);
        Assert.That(t.Connections["app1"], Is.EqualTo("conn1"));
    }

    [Test]
    public void GetConnectionString_Throws_When_No_Match()
    {
        var t = new Tenant("abc");
        t.TryAddConnection("abc_WebService",
            "Server=myServerAddress;Database=myDataBase;User Id=u;Password=p;");

        Assert.Throws<TownSuiteException>(() => t.GetConnectionString("DoesNotExist"));
    }

    [Test]
    public void GetConnectionString_Matches_Substring_CaseInsensitive()
    {
        const string cs = "Server=myServerAddress;Database=myDataBase;User Id=u;Password=p;";
        var t = new Tenant("abc");
        t.TryAddConnection("abc_WebService", cs);

        Assert.That(t.GetConnectionString("webservice"), Is.EqualTo(cs));
    }
}
