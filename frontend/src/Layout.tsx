import { Link, useLocation } from 'react-router-dom';
import { LayoutDashboard, FileText, Activity, Layers, Settings, LogOut, Search, ArrowRight } from 'lucide-react';

const navItems = [
  { icon: LayoutDashboard, label: 'Dashboard', path: '/' },
  { icon: FileText, label: 'Papers', path: '/papers' },
  { icon: Activity, label: 'Jobs', path: '/jobs' },
  { icon: Layers, label: 'Analysis', path: '/analysis' },
  { icon: Settings, label: 'Settings', path: '/settings' },
];

export function Layout({ children }: { children: React.ReactNode }) {
  const location = useLocation();

  const currentPageLabel = location.pathname === '/' 
    ? 'Dashboard' 
    : location.pathname.split('/')[1];

  return (
    <div className="flex h-screen w-full overflow-hidden bg-white text-black font-sans">
      {/* Sidebar — black background, white text, no decorations */}
      <aside className="w-[240px] flex-shrink-0 flex flex-col bg-black text-white border-r-0">
        {/* Logo / Brand */}
        <div className="flex items-center h-[64px] px-[24px] border-b border-white/10">
          <div className="flex items-center gap-[12px]">
            <ArrowRight size={18} strokeWidth={2.5} className="text-white" />
            <span className="text-[12px] font-bold tracking-[0.08em] uppercase text-white">
              Research Agent
            </span>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 py-[32px] px-[16px] space-y-[2px] overflow-y-auto">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path || (item.path !== '/' && location.pathname.startsWith(item.path));
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center gap-[12px] px-[12px] py-[10px] transition-colors duration-150 text-[12px] font-bold tracking-[0.08em] uppercase ${
                  isActive
                    ? 'bg-white text-black'
                    : 'text-white/60 hover:text-white hover:bg-white/5'
                }`}
              >
                <Icon size={16} strokeWidth={isActive ? 2.5 : 1.5} />
                {item.label}
              </Link>
            );
          })}
        </nav>

        {/* Sign out */}
        <div className="p-[16px] border-t border-white/10">
          <button className="flex w-full items-center gap-[12px] px-[12px] py-[10px] text-[12px] font-bold tracking-[0.08em] uppercase text-white/40 hover:text-swiss-red transition-colors">
            <LogOut size={16} strokeWidth={1.5} />
            <span>Sign out</span>
          </button>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 flex flex-col h-screen overflow-hidden bg-white">
        {/* Header — minimal top bar */}
        <header className="h-[64px] flex items-center justify-between px-[48px] border-b border-black/10 bg-white">
          <div className="flex items-center gap-[16px]">
            <span className="text-[12px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500">
              {currentPageLabel}
            </span>
          </div>

          <div className="flex items-center gap-[24px]">
            {/* Search */}
            <div className="relative">
              <Search className="absolute left-[12px] top-1/2 -translate-y-1/2 text-swiss-grey-300" size={14} />
              <input
                type="text"
                placeholder="Search…"
                className="w-[240px] h-[36px] pl-[36px] pr-[16px] bg-swiss-grey-100 border border-swiss-grey-200 text-[14px] font-light text-black placeholder:text-swiss-grey-300 focus:outline-none focus:border-swiss-blue transition-colors"
              />
            </div>
            {/* User avatar — flat square */}
            <div className="w-[32px] h-[32px] bg-black flex items-center justify-center cursor-pointer">
              <span className="text-[10px] font-bold text-white tracking-[0.12em]">AD</span>
            </div>
          </div>
        </header>

        {/* Scrollable Page Content */}
        <div className="flex-1 overflow-auto p-[48px] bg-white">
          {children}
        </div>
      </main>
    </div>
  );
}
