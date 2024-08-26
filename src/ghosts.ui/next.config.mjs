/** @type {import('next').NextConfig} */
const nextConfig = {
	// Export as a standalone nodejs server for use within a Docker container
	output: "standalone",
};

export default nextConfig;
