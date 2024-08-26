import { ThemeProvider } from "@/components/theme-provider";
import { ThemeToggle } from "@/components/theme-toggle";
import { Toaster } from "@/components/ui/toaster";
import { Inter } from "next/font/google";
import Link from "next/link";
import "./globals.css";
import Image from "next/image";
import GLogo from "../../public/logo.png";

const inter = Inter({ subsets: ["latin"] });

export const dynamic = "force-dynamic";

export default function RootLayout({
	children,
}: Readonly<{
	children: React.ReactNode;
}>) {
	return (
		<html lang="en">
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
