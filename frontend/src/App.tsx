import { Link, Route, Routes } from 'react-router-dom';
import { Layout } from './Layout';
import { Dashboard } from './pages/Dashboard';
import { Papers } from './pages/Papers';
import { Jobs } from './pages/Jobs';

function PlaceholderPage({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex items-start justify-start h-full pt-[80px]">
      <div className="space-y-[16px]">
        <h1 className="text-[4.209rem] font-black leading-[1.1] tracking-[-0.02em] text-black">
          {title}
        </h1>
        <p className="text-[14px] font-light text-swiss-grey-500 max-w-[480px]">
          {description}
        </p>
        <div className="w-[64px] h-[4px] bg-swiss-blue mt-[24px]" />
      </div>
    </div>
  );
}

function NotFoundPage() {
  return (
    <div className="flex items-start justify-start h-full pt-[80px]">
      <div className="space-y-[16px]">
        <p className="text-[12px] font-bold tracking-[0.08em] uppercase text-swiss-grey-500">
          404
        </p>
        <h1 className="text-[4.209rem] font-black leading-[1.1] tracking-[-0.02em] text-black">
          Page not found
        </h1>
        <p className="text-[14px] font-light text-swiss-grey-500 max-w-[520px]">
          This URL does not match a page in the research workspace. Use the dashboard or papers view to get back on track.
        </p>
        <div className="flex items-center gap-[12px] pt-[8px]">
          <Link
            to="/"
            className="inline-flex items-center justify-center h-[40px] px-[18px] bg-black text-white text-[12px] font-bold tracking-[0.08em] uppercase transition-colors hover:bg-swiss-blue"
          >
            Go to dashboard
          </Link>
          <Link
            to="/papers"
            className="inline-flex items-center justify-center h-[40px] px-[18px] border border-black text-black text-[12px] font-bold tracking-[0.08em] uppercase transition-colors hover:bg-black hover:text-white"
          >
            Browse papers
          </Link>
        </div>
        <div className="w-[64px] h-[4px] bg-swiss-red mt-[24px]" />
      </div>
    </div>
  );
}

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/papers" element={<Papers />} />
        <Route path="/jobs" element={<Jobs />} />
        <Route path="/analysis" element={<PlaceholderPage title="Deep Analysis" description="LLM insights and visualizations coming soon." />} />
        <Route path="/settings" element={<PlaceholderPage title="Settings" description="Configuration panel is under construction." />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Layout>
  );
}
