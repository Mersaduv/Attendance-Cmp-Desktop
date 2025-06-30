using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using Microsoft.EntityFrameworkCore;

namespace AttandenceDesktop.Services;

public class DeviceService
{
    private readonly Func<ApplicationDbContext> _ctxFactory;
    private readonly DataRefreshService _refresh;
    public DeviceService(Func<ApplicationDbContext> ctxFactory, DataRefreshService refresh)
    {
        _ctxFactory = ctxFactory;
        _refresh = refresh;
    }

    private ApplicationDbContext New()=> _ctxFactory();

    public async Task<List<Device>> GetAllAsync()
    {
        using var ctx = New();
        return await ctx.Devices.ToListAsync();
    }

    public async Task AddAsync(Device d)
    {
        using var ctx = New();
        ctx.Devices.Add(d);
        await ctx.SaveChangesAsync();
        _refresh.NotifyDevicesChanged();
    }

    public async Task UpdateAsync(Device d)
    {
        using var ctx = New();
        ctx.Devices.Update(d);
        await ctx.SaveChangesAsync();
        _refresh.NotifyDevicesChanged();
    }

    public async Task DeleteAsync(int id)
    {
        using var ctx = New();
        var dev = await ctx.Devices.FindAsync(id);
        if (dev != null)
        {
            ctx.Devices.Remove(dev);
            await ctx.SaveChangesAsync();
            _refresh.NotifyDevicesChanged();
        }
    }
} 