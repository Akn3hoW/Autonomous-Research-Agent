import { useMemo, useState } from "react";
import { Download, FileText, Filter, MoreHorizontal, Plus, Search } from "lucide-react";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";

const papersData = [
  { id: "P-1002", title: "Attention Is All You Need", authors: "Vaswani et al.", year: 2017, status: "Analyzed" },
  { id: "P-1003", title: "BERT: Pre-training of Deep Bidirectional Transformers", authors: "Devlin et al.", year: 2018, status: "Pending" },
  { id: "P-1004", title: "GPT-3: Language Models are Few-Shot Learners", authors: "Brown et al.", year: 2020, status: "Summarized" },
  { id: "P-1005", title: "Training language models to follow instructions", authors: "Ouyang et al.", year: 2022, status: "Failed" },
  { id: "P-1006", title: "Llama 2: Open Foundation and Fine-Tuned Chat Models", authors: "Touvron et al.", year: 2023, status: "Analyzed" },
];

function getStatusColor(status: string): string {
  switch (status) {
    case "Analyzed": return "text-black";
    case "Pending": return "text-swiss-grey-500";
    case "Failed": return "text-swiss-red";
    case "Summarized": return "text-swiss-blue";
    default: return "text-black";
  }
}

function getStatusIndicator(status: string): string {
  switch (status) {
    case "Analyzed": return "bg-black";
    case "Pending": return "bg-swiss-yellow";
    case "Failed": return "bg-swiss-red";
    case "Summarized": return "bg-swiss-blue";
    default: return "bg-swiss-grey-300";
  }
}

export function Papers() {
  const [searchTerm, setSearchTerm] = useState("");

  const filteredPapers = useMemo(() => {
    const term = searchTerm.toLowerCase();
    return papersData.filter(p =>
      p.title.toLowerCase().includes(term) ||
      p.authors.toLowerCase().includes(term)
    );
  }, [searchTerm]);

  return (
    <div className="space-y-[48px]">
      {/* Header — asymmetric */}
      <div className="grid grid-cols-12 gap-[24px] items-end">
        <div className="col-span-7">
          <h1 className="text-[4.209rem] font-black leading-[1.1] tracking-[-0.02em] text-black">
            Papers
          </h1>
          <p className="text-[14px] font-light text-swiss-grey-500 mt-[8px]">
            Manage and analyze your ingested scientific papers.
          </p>
        </div>
        <div className="col-span-5 flex justify-end gap-[8px]">
          <button className="flex items-center gap-[8px] px-[24px] py-[12px] border-2 border-black bg-transparent text-black text-[11px] font-bold tracking-[0.08em] uppercase hover:bg-black hover:text-white transition-colors duration-150">
            <Download size={14} />
            Export
          </button>
          <button className="flex items-center gap-[8px] px-[24px] py-[12px] bg-black text-white text-[11px] font-bold tracking-[0.08em] uppercase border-2 border-black hover:bg-transparent hover:text-black transition-colors duration-150">
            <Plus size={14} />
            Import Papers
          </button>
        </div>
      </div>

      {/* Search + Filter bar */}
      <div className="flex gap-[8px]">
        <div className="relative flex-1 max-w-[480px]">
          <Search className="absolute left-[12px] top-1/2 -translate-y-1/2 text-swiss-grey-300" size={14} />
          <input
            type="text"
            placeholder="Filter by title, author…"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full h-[40px] pl-[36px] pr-[16px] bg-swiss-grey-100 border border-swiss-grey-200 text-[14px] font-light text-black placeholder:text-swiss-grey-300 focus:outline-none focus:border-swiss-blue transition-colors"
          />
        </div>
        <button className="flex items-center gap-[8px] px-[16px] h-[40px] border border-swiss-grey-200 bg-white text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 hover:border-black hover:text-black transition-colors">
          <Filter size={14} />
          Filters
        </button>
      </div>

      {/* Papers Table */}
      <div className="border border-swiss-grey-200">
        <table className="w-full">
          <thead>
            <tr className="border-b border-swiss-grey-200 bg-swiss-grey-100">
              <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px] w-[100px]">ID</th>
              <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Title</th>
              <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Authors</th>
              <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Year</th>
              <th className="text-left text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Status</th>
              <th className="text-right text-[11px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500 py-[12px] px-[24px]">Actions</th>
            </tr>
          </thead>
          <tbody>
            {filteredPapers.length > 0 ? (
              filteredPapers.map((paper) => (
                <tr key={paper.id} className="border-b border-swiss-grey-100 hover:bg-swiss-grey-100 transition-colors group">
                  <td className="py-[14px] px-[24px] font-mono text-[12px] font-bold text-swiss-grey-500 group-hover:text-black transition-colors">
                    {paper.id}
                  </td>
                  <td className="py-[14px] px-[24px]">
                    <div className="flex items-center gap-[12px]">
                      <FileText size={14} className="text-swiss-grey-300 group-hover:text-swiss-blue transition-colors flex-shrink-0" />
                      <span className="text-[14px] font-normal text-black">{paper.title}</span>
                    </div>
                  </td>
                  <td className="py-[14px] px-[24px] text-[14px] font-light text-swiss-grey-500">{paper.authors}</td>
                  <td className="py-[14px] px-[24px] text-[14px] font-light text-swiss-grey-500">{paper.year}</td>
                  <td className="py-[14px] px-[24px]">
                    <span className={`inline-flex items-center gap-[6px] text-[11px] font-bold tracking-[0.08em] uppercase ${getStatusColor(paper.status)}`}>
                      <span className={`w-[6px] h-[6px] ${getStatusIndicator(paper.status)}`} />
                      {paper.status}
                    </span>
                  </td>
                  <td className="py-[14px] px-[24px] text-right">
                    <DropdownMenu>
                      <DropdownMenuTrigger className="h-[32px] w-[32px] inline-flex items-center justify-center hover:bg-swiss-grey-100 text-swiss-grey-300 hover:text-black transition-colors">
                        <span className="sr-only">Open menu</span>
                        <MoreHorizontal size={16} />
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end" className="bg-white border border-swiss-grey-200 text-black min-w-[160px]">
                        <DropdownMenuItem className="text-[12px] font-bold tracking-[0.04em] uppercase cursor-pointer hover:bg-swiss-grey-100 focus:bg-swiss-grey-100">
                          View Details
                        </DropdownMenuItem>
                        <DropdownMenuItem className="text-[12px] font-bold tracking-[0.04em] uppercase cursor-pointer hover:bg-swiss-grey-100 focus:bg-swiss-grey-100">
                          Generate Summary
                        </DropdownMenuItem>
                        <DropdownMenuItem className="text-[12px] font-bold tracking-[0.04em] uppercase cursor-pointer text-swiss-red hover:bg-swiss-grey-100 focus:bg-swiss-grey-100">
                          Delete
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} className="h-[128px] text-center text-[14px] font-light text-swiss-grey-500">
                  No papers found matching your search.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
