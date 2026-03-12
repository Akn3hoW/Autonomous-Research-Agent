import { Activity, Play, RotateCcw, StopCircle, Clock, CheckCircle, XCircle } from "lucide-react";

const jobsData = [
  { id: "J-041", type: "summarize-paper", status: "Running", progress: 65, startTime: "2 min ago", target: "P-1002" },
  { id: "J-040", type: "import-papers", status: "Completed", progress: 100, startTime: "1 hour ago", target: "Batch 42" },
  { id: "J-039", type: "generate-insights", status: "Completed", progress: 100, startTime: "5 hours ago", target: "All Papers" },
  { id: "J-038", type: "summarize-paper", status: "Failed", progress: 12, startTime: "Yesterday", target: "P-1005" },
  { id: "J-037", type: "import-papers", status: "Completed", progress: 100, startTime: "2 days ago", target: "Batch 41" },
];

function getProgressColor(status: string): string {
  switch (status) {
    case "Completed": return "var(--swiss-black)";
    case "Running": return "var(--swiss-blue)";
    case "Failed": return "var(--swiss-red)";
    default: return "var(--swiss-grey-300)";
  }
}

function getStatusIcon(status: string) {
  switch (status) {
    case "Running": return <Clock size={12} />;
    case "Completed": return <CheckCircle size={12} />;
    case "Failed": return <XCircle size={12} />;
    default: return null;
  }
}

function getStatusTextColor(status: string): string {
  switch (status) {
    case "Running": return "text-swiss-blue";
    case "Failed": return "text-swiss-red";
    default: return "text-black";
  }
}

export function Jobs() {
  const activeWorkers = jobsData.filter(j => j.status === "Running").length;
  const totalWorkers = 10;

  return (
    <div className="space-y-[48px]">
      {/* Header — asymmetric layout */}
      <div className="grid grid-cols-12 gap-[24px] items-end">
        <div className="col-span-7">
          <h1 className="text-[4.209rem] font-black leading-[1.1] tracking-[-0.02em] text-black">
            Jobs
          </h1>
          <p className="text-[14px] font-light text-swiss-grey-500 mt-[8px]">
            Monitor background tasks and system processes.
          </p>
        </div>
        <div className="col-span-5 flex justify-end gap-[8px]">
          <button className="flex items-center gap-[8px] px-[24px] py-[12px] border-2 border-black bg-transparent text-black text-[11px] font-bold tracking-[0.08em] uppercase hover:bg-black hover:text-white transition-colors duration-150">
            <StopCircle size={14} />
            Pause All
          </button>
          <button className="flex items-center gap-[8px] px-[24px] py-[12px] bg-black text-white text-[11px] font-bold tracking-[0.08em] uppercase border-2 border-black hover:bg-transparent hover:text-black transition-colors duration-150">
            <Play size={14} />
            Run New Job
          </button>
        </div>
      </div>

      {/* Overview strip — asymmetric 3 / 9 split */}
      <div className="grid grid-cols-12 gap-[24px]">
        {/* Active Workers — 3 columns */}
        <div className="col-span-3 border border-swiss-grey-200 p-[24px] bg-white">
          <div className="flex items-start justify-between mb-[16px]">
            <span className="text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500">
              Active Workers
            </span>
            <Activity size={16} strokeWidth={1.5} className="text-swiss-grey-300" />
          </div>
          <div className="text-[3.157rem] font-black leading-[1] tracking-[-0.02em] text-black">
            {activeWorkers}/{totalWorkers}
          </div>
          <p className="text-[12px] font-light text-swiss-grey-500 mt-[8px]">
            Ready for requests
          </p>
          {/* Visual worker utilization bar */}
          <div className="mt-[16px] flex gap-[4px]">
            {Array.from({ length: totalWorkers }).map((_, i) => (
              <div
                key={i}
                className={`flex-1 h-[8px] ${i < activeWorkers ? 'bg-swiss-blue' : 'bg-swiss-grey-200'}`}
              />
            ))}
          </div>
        </div>

        {/* Job Queue — 9 columns */}
        <div className="col-span-9 border border-swiss-grey-200">
          <div className="p-[24px] border-b border-swiss-grey-200">
            <h2 className="text-[1.777rem] font-black tracking-[-0.02em] text-black">
              Job Queue
            </h2>
            <p className="text-[12px] font-light text-swiss-grey-500 mt-[4px]">
              Live monitoring of background operations
            </p>
          </div>

          <table className="w-full">
            <thead>
              <tr className="border-b border-swiss-grey-200 bg-swiss-grey-100">
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px] w-[80px]">ID</th>
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Type</th>
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Target</th>
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px] w-[200px]">Progress</th>
                <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Status</th>
                <th className="text-right text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Actions</th>
              </tr>
            </thead>
            <tbody>
              {jobsData.map((job) => (
                <tr key={job.id} className="border-b border-swiss-grey-100 hover:bg-swiss-grey-100 transition-colors group">
                  <td className="py-[14px] px-[24px] font-mono text-[12px] font-bold text-swiss-grey-500 group-hover:text-black transition-colors">
                    {job.id}
                  </td>
                  <td className="py-[14px] px-[24px] text-[14px] font-normal text-black">
                    {job.type}
                  </td>
                  <td className="py-[14px] px-[24px] text-[14px] font-light text-swiss-grey-500">
                    {job.target}
                  </td>
                  <td className="py-[14px] px-[24px]">
                    <div className="flex items-center gap-[12px]">
                      <div className="flex-1 h-[4px] bg-swiss-grey-200">
                        <div
                          className="h-full transition-all duration-700"
                          style={{
                            width: `${job.progress}%`,
                            backgroundColor: getProgressColor(job.status),
                          }}
                        />
                      </div>
                      <span className="text-[11px] font-mono font-bold text-swiss-grey-500 w-[32px] text-right">
                        {job.progress}%
                      </span>
                    </div>
                  </td>
                  <td className="py-[14px] px-[24px]">
                    <span className={`inline-flex items-center gap-[6px] text-[11px] font-bold tracking-[0.08em] uppercase ${getStatusTextColor(job.status)}`}>
                      {getStatusIcon(job.status)}
                      {job.status}
                    </span>
                  </td>
                  <td className="py-[14px] px-[24px] text-right">
                    {job.status === "Failed" ? (
                      <button
                        className="inline-flex items-center gap-[6px] px-[12px] py-[6px] border border-swiss-grey-200 text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 hover:border-black hover:text-black transition-colors"
                        title="Retry Job"
                      >
                        <RotateCcw size={12} />
                        Retry
                      </button>
                    ) : (
                      <span className="text-[12px] font-light text-swiss-grey-300 font-mono">
                        {job.startTime}
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
