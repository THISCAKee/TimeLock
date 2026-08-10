import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "TimeLock Admin",
  description: "TimeLock remote administration panel",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="th"><body>{children}</body></html>;
}
