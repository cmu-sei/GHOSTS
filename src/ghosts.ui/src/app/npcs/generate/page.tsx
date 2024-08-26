import { SheetWrapper } from "@/components/sheet-wrapper";
import { NpcGenerator } from "./npc-generator";

export default function Page() {
	return (
		<SheetWrapper returnPath="/npcs">
			<NpcGenerator />
		</SheetWrapper>
	);
}
