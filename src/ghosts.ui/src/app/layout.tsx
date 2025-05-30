export const dynamic = "force-dynamic";

import { Inter } from "next/font/google";
import { ThemeProvider } from "@/components/theme-provider";
import { ThemeToggle } from "@/components/theme-toggle";
import { Toaster } from "@/components/ui/toaster";
import Link from "next/link";
import Image from "next/image";
import GLogo from "../../public/logo.png";
import "./globals.css";

const inter = Inter({ subsets: ["latin"] });

export default function RootLayout({ children }: { children: React.ReactNode }) {
	return (
		<html lang="en" suppressHydrationWarning>
			<body className={inter.className}>
				<ThemeProvider
					attribute="class"
					defaultTheme="system"
					enableSystem
					disableTransitionOnChange
				>
					<nav className="fixed left-0 top-0 w-full flex items-center justify-start gap-4 px-16 py-4 bg-muted z-10">
						<Image src={GLogo} alt="Ghosts Logo" width={64} height={64} />
						<Link href="/machines">Machines</Link>
						<Link href="/machine-groups">Machine Groups</Link>
						<Link href="/timelines">TimeLines</Link>
						<Link href="/npcs">Npcs</Link>
						<ThemeToggle />
					</nav>
					<main className="flex flex-col w-full items-start justify-center gap-8 mt-32 p-16">
						{children}
					</main>
					<Toaster />
				</ThemeProvider>
			</body>
		</html>
	);
}
