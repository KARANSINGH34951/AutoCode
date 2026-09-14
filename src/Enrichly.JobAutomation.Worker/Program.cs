using Enrichly.JobAutomation.Infrastructure.Persistence;
using Enrichly.JobAutomation.Infrastructure.Services;
using Enrichly.JobAutomation.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<ReliabilityOptions>(builder.Configuration.GetSection(ReliabilityOptions.SectionName));
builder.Services.AddDbContext<JobAutomationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
builder.Services.AddScoped<ExecutionClaimService>();
builder.Services.AddScoped<ExecutionBackoffService>();
builder.Services.AddHttpClient("webhooks").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<HourlyJobSchedulerService>();
builder.Services.AddHostedService<StaleExecutionReclaimerService>();

var host = builder.Build();
host.Run();
