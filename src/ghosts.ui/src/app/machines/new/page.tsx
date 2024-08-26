import { SheetWrapper } from "@/components/sheet-wrapper";
import { MachineForm } from "./machine-form";

export default function Page() {
	return (
		<SheetWrapper returnPath="/machines">
			<MachineForm />
		</SheetWrapper>
	);
}
