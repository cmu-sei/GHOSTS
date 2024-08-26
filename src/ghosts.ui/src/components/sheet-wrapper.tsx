"use client";

import { useRouter } from "next/navigation";
import { type PropsWithChildren, useEffect, useState } from "react";
import { Sheet, SheetContent } from "./ui/sheet";

/**
 * A component that wraps a anything in a sheet sidebar
 */
export function SheetWrapper({
	returnPath,
	children,
	side,
}: { returnPath: string; side?: "right" | "left" } & PropsWithChildren) {
	const router = useRouter();

	const [open, setOpen] = useState(false);

	//  Open automatically because this component is used as an overlay with other pages behind
	useEffect(() => {
		setOpen(true);
	}, []);

	return (
		<Sheet
			open={open}
			onOpenChange={(open) => {
				// We store whether the form is open in the url state so to close we go back
				if (!open) router.push(returnPath);
			}}
		>
			<SheetContent
				side={side ?? "left"}
				className="w-[800px] max-w-full flex flex-col gap-4 p-16 overflow-y-scroll sm:max-w-screen"
			>
				{children}
			</SheetContent>
		</Sheet>
	);
}
