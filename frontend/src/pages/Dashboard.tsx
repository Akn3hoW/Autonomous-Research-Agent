import { Activity, Beaker, CheckCircle, Clock, Database, Layers, ArrowUpRight, ArrowDownRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export function Dashboard() {
  const stats = [
    { title: "Total Papers", value: "2,543", change: "+14.5%", positive: true, icon: Database },
    { title: "Active Jobs", value: "12", change: "+2", positive: true, icon: Activity },
    { title: "Summaries", value: "1,200", change: "+48%", positive: true, icon: Layers },
    { title: "Analysis Run", value: "85", change: "−4%", positive: false, icon: Beaker },
  ];

  const recentJobs = [
    { id: "J-041", type: "summarize-paper", status: "Running", time: "2 min ago" },
    { id: "J-040", type: "import-papers", status: "Completed", time: "1 hour ago" },
    { id: "J-039", type: "generate-insights", status: "Completed", time: "5 hours ago" },
    { id: "J-038", type: "summarize-paper", status: "Failed", time: "Yesterday" },
    { id: "J-037", type: "import-papers", status: "Completed", time: "2 days ago" },
  ];

  const systemMetrics = [
    { label: "API Status", value: "99.9% Uptime", percentage: 99.9, accentColor: "var(--swiss-blue)" },
    { label: "Database CPU", value: "42%", percentage: 42, accentColor: "var(--swiss-black)" },
    { label: "Storage Used", value: "75%", percentage: 75, accentColor: "var(--swiss-yellow)" },
  ];

  return (
    <div className="space-y-[48px]">
      {/* Page Header — asymmetric, left-aligned */}
      <div>
        <h1 className="text-[4.209rem] font-black leading-[1.1] tracking-[-0.02em] text-black">
          Dashboard
        </h1>
        <p className="text-[14px] font-light text-swiss-grey-500 mt-[8px] max-w-[480px]">
          Monitor your data ingestion, jobs, and analysis status at a glance.
        </p>
      </div>

      {/* Stats Row — 4 columns with strong typographic hierarchy */}
      <div className="grid grid-cols-4 gap-[24px]">
        {stats.map((stat, i) => {
          const Icon = stat.icon;
          return (
            <div key={i} className="border border-swiss-grey-200 p-[24px] bg-white hover:border-black transition-colors duration-150 group">
              <div className="flex items-start justify-between mb-[16px]">
                <span className="text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500">
                  {stat.title}
                </span>
                <Icon size={16} strokeWidth={1.5} className="text-swiss-grey-300 group-hover:text-black transition-colors" />
              </div>
              <div className="text-[3.157rem] font-black leading-[1] tracking-[-0.02em] text-black">
                {stat.value}
              </div>
              <div className="flex items-center gap-[4px] mt-[12px]">
                {stat.positive ? (
                  <ArrowUpRight size={14} className="text-swiss-blue" />
                ) : (
                  <ArrowDownRight size={14} className="text-swiss-red" />
                )}
                <span className={`text-[12px] font-bold ${stat.positive ? 'text-swiss-blue' : 'text-swiss-red'}`}>
                  {stat.change}
                </span>
                <span className="text-[12px] font-light text-swiss-grey-300">from last month</span>
              </div>
            </div>
          );
        })}
      </div>

      {/* Two-column asymmetric layout: 8 / 4 split */}
      <div className="grid grid-cols-12 gap-[24px]">
        {/* Recent Jobs — 8 columns */}
        <div className="col-span-8 border border-swiss-grey-200">
          <div className="p-[24px] border-b border-swiss-grey-200">
            <h2 className="text-[1.777rem] font-black tracking-[-0.02em] text-black">
              Recent Jobs
            </h2>
            <p className="text-[12px] font-light text-swiss-grey-500 mt-[4px]">
              Live status of your recent background processes
            </p>
          </div>

          <table className="w-full">
            <thead>
              <tr className="border-b border-swiss-grey-200 bg-swiss-grey-100">
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">ID</th>
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Type</th>
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Status</th>
                <th className="text-right text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Started</th>
              </tr>
            </thead>
            <tbody>
              {recentJobs.map((job) => (
                <tr key={job.id} className="border-b border-swiss-grey-100 hover:bg-swiss-grey-100 transition-colors">
                  <td className="py-[12px] px-[24px] font-mono text-[12px] font-bold text-black">{job.id}</td>
                  <td className="py-[12px] px-[24px] text-[14px] font-light text-black">{job.type}</td>
                  <td className="py-[12px] px-[24px]">
                    <span className={`inline-flex items-center gap-[6px] text-[11px] font-bold tracking-[0.08em] uppercase ${
                      job.status === "Completed" ? "text-black" :
                      job.status === "Running" ? "text-swiss-blue" :
                      "text-swiss-red"
                    }`}>
                      {job.status === "Running" && <Clock size={12} />}
                      {job.status === "Completed" && <CheckCircle size={12} />}
                      {job.status}
                    </span>
                  </td>
                  <td className="py-[12px] px-[24px] text-right text-[12px] font-light text-swiss-grey-500">{job.time}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* System Health — 4 columns */}
        <div className="col-span-4 border border-swiss-grey-200">
          <div className="p-[24px] border-b border-swiss-grey-200">
            <h2 className="text-[1.777rem] font-black tracking-[-0.02em] text-black">
              System Health
            </h2>
            <p className="text-[12px] font-light text-swiss-grey-500 mt-[4px]">
              API and Database Metrics
            </p>
          </div>

          <div className="p-[24px] space-y-[32px]">
            {systemMetrics.map((metric) => (
              <div key={metric.label} className="space-y-[8px]">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500">
                    {metric.label}
                  </span>
                  <span className="text-[14px] font-bold text-black">{metric.value}</span>
                </div>
                {/* Flat progress bar — no rounded corners */}
                <div className="h-[4px] bg-swiss-grey-200 w-full">
                  <div
                    className="h-full transition-all duration-500"
                    style={{
                      width: `${metric.percentage}%`,
                      backgroundColor: metric.accentColor,
                    }}
                  />
                </div>
              </div>
            ))}

            <div className="pt-[16px] border-t border-swiss-grey-200">
              <Badge
                variant="outline"
                className="text-[11px] font-bold tracking-[0.08em] uppercase border-black text-black bg-transparent hover:bg-black hover:text-white transition-colors cursor-default"
              >
                All Systems Operational
              </Badge>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
