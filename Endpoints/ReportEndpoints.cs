using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using UserManagementApi.Services;

namespace UserManagementApi.Endpoints;

public static class ReportEndpoints
{
    public static WebApplication MapReportEndpoints(this WebApplication app) {
        var g = app.MapGroup("/reports").WithTags("Reports");

        g.MapGet("/latest-month-names",
            async (IReportsService svc) => {
                var names = await svc.GetLatestMonthNamesAsync();
                return Results.Ok(names);
            })
         .WithName("GetLatestMonthNames");

        g.MapGet("/duplicate-names",
            async (IReportsService svc) => {
                var names = await svc.GetDuplicateNamesAsync();
                return Results.Ok(names);
            })
         .WithName("GetDuplicateNames");

        g.MapGet("/count-users-with-cars",
            async (IReportsService svc) => {
                var count = await svc.CountUsersWithCarsAsync();
                return Results.Ok(new { Count = count });
            })
         .WithName("CountUsersWithCars");

        g.MapGet("/count-cars-without-users",
            async (IReportsService svc) => {
                var count = await svc.CountCarsWithoutUsersAsync();
                return Results.Ok(new { Count = count });
            })
         .WithName("CountCarsWithoutUsers");

        // 5. export→SFTP→import, then get duplicates from DB
        g.MapPost("/migrate-duplicate-names/{table}/{localPath}/{remotePath}",
            async (string table,
                   string localPath,
                   string remotePath,
                   IReportsService svc) => {
                       var result = await svc.MigrateAndGetDuplicateNamesAsync(
                               table, localPath, remotePath);
                       return Results.Ok(result);
                   })
         .WithName("MigrateAndGetDuplicateNames");

        // 6. download CSV via SFTP, then get duplicates from the file
        g.MapPost("/csv-duplicate-names/{remotePath}/{localPath}",
            async (string remotePath,
                   string localPath,
                   IReportsService svc) => {
                       var result = await svc.GetDuplicateNamesFromCsvAsync(
                               remotePath, localPath);
                       return Results.Ok(result);
                   })
         .WithName("GetDuplicateNamesFromCsv");

        return app;
    }
}
