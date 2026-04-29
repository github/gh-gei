package bbs

import (
	"fmt"
	"io"
	"os"

	"github.com/pkg/sftp"
	"golang.org/x/crypto/ssh"
)

// realSFTPClient wraps *sftp.Client to satisfy the sftpClient interface.
type realSFTPClient struct {
	sshConn *ssh.Client
	sftp    *sftp.Client
}

func newRealSFTPClient(host, user, privateKeyPath string, port int) (*realSFTPClient, error) {
	keyBytes, err := os.ReadFile(privateKeyPath)
	if err != nil {
		return nil, fmt.Errorf("read private key: %w", err)
	}

	signer, err := ssh.ParsePrivateKey(keyBytes)
	if err != nil {
		return nil, fmt.Errorf("parse private key: %w", err)
	}

	config := &ssh.ClientConfig{
		User: user,
		Auth: []ssh.AuthMethod{
			ssh.PublicKeys(signer),
		},
		HostKeyCallback: ssh.InsecureIgnoreHostKey(), //nolint:gosec // BBS servers are internal
	}

	addr := fmt.Sprintf("%s:%d", host, port)
	sshConn, err := ssh.Dial("tcp", addr, config)
	if err != nil {
		return nil, fmt.Errorf("ssh connect to %s: %w", addr, err)
	}

	sftpConn, err := sftp.NewClient(sshConn)
	if err != nil {
		sshConn.Close()
		return nil, fmt.Errorf("sftp session: %w", err)
	}

	return &realSFTPClient{sshConn: sshConn, sftp: sftpConn}, nil
}

func (c *realSFTPClient) Stat(path string) (os.FileInfo, error) {
	return c.sftp.Stat(path)
}

func (c *realSFTPClient) Open(path string) (io.ReadCloser, error) {
	return c.sftp.Open(path)
}

func (c *realSFTPClient) Close() error {
	sftpErr := c.sftp.Close()
	sshErr := c.sshConn.Close()
	if sftpErr != nil {
		return sftpErr
	}
	return sshErr
}
