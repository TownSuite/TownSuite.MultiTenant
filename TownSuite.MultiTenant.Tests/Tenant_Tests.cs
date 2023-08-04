using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace TownSuite.MultiTenant.Tests;

public class Tenant_Tests
{
    [Test]
    public void Clone_Test()
    {
        var t = new Tenant("abc");
        t.Connections.Add("app1", "conn1");
        t.Connections.Add("app2", "conn2");
        
        var clone = t.Clone() as Tenant;
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.UniqueId, Is.EqualTo(t.UniqueId));
        Assert.That(clone.Connections.Count, Is.EqualTo(t.Connections.Count));
        Assert.That(clone.Connections["app1"], Is.EqualTo(t.Connections["app1"]));
        Assert.That(clone.Connections["app2"], Is.EqualTo(t.Connections["app2"]));
    }
    
    [Test]
    public void Equals_Test()
    {
        var t1 = new Tenant("abc");
        t1.Connections.Add("app1", "conn1");
        t1.Connections.Add("app2", "conn2");
        
        var t2 = new Tenant("abc");
        t2.Connections.Add("app1", "conn1");
        t2.Connections.Add("app2", "conn2");
        
        Assert.That(t1.Equals(t2), Is.True);
    }
}