import { SheetWrapper } from "@/components/sheet-wrapper";
import { NpcForm } from "./npc-form";

export default function Page() {
	return (
		<SheetWrapper returnPath="/npcs">
			<NpcForm />
		</SheetWrapper>
	);
}
