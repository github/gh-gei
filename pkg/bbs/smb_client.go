package bbs

import (
	"context"
	"fmt"
	"io"
	"net"

	"github.com/hirochachacha/go-smb2"
)

// realSMBConnector implements smbConnector using the go-smb2 library.
type realSMBConnector struct {
	conn    net.Conn
	session *smb2.Session
}

func (c *realSMBConnector) Connect(host string) error {
	var d net.Dialer
	conn, err := d.DialContext(context.Background(), "tcp", host+":445")
	if err != nil {
		return fmt.Errorf("dial %s:445: %w", host, err)
	}
	c.conn = conn
	return nil
}

func (c *realSMBConnector) Login(user, password, domain string) error {
	d := &smb2.Dialer{
		Initiator: &smb2.NTLMInitiator{
			User:     user,
			Password: password,
			Domain:   domain,
		},
	}
	session, err := d.Dial(c.conn)
	if err != nil {
		return fmt.Errorf("smb2 login: %w", err)
	}
	c.session = session
	return nil
}

func (c *realSMBConnector) Mount(shareName string) (smbShare, error) {
	share, err := c.session.Mount(shareName)
	if err != nil {
		return nil, fmt.Errorf("mount share %s: %w", shareName, err)
	}
	return &realSMBShare{share: share}, nil
}

func (c *realSMBConnector) Logoff() error {
	if c.session != nil {
		return c.session.Logoff()
	}
	return nil
}

func (c *realSMBConnector) Close() error {
	if c.conn != nil {
		return c.conn.Close()
	}
	return nil
}

// realSMBShare wraps *smb2.Share to satisfy the smbShare interface.
type realSMBShare struct {
	share *smb2.Share
}

func (s *realSMBShare) Open(name string) (io.ReadCloser, error) {
	return s.share.Open(name)
}

func (s *realSMBShare) Stat(name string) (int64, error) {
	info, err := s.share.Stat(name)
	if err != nil {
		return 0, err
	}
	return info.Size(), nil
}
