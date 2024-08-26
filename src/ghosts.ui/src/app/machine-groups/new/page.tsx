import { SheetWrapper } from "@/components/sheet-wrapper";
import { MachineGroupForm } from "./machine-group-form";

export default function Page() {
	return (
		<SheetWrapper returnPath="/machine-groups">
			<MachineGroupForm />
		</SheetWrapper>
	);
}
