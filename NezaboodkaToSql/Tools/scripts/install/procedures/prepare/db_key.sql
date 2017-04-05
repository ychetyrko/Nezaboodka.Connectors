CREATE TABLE `${db_name}`.`db_key` (
	`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
	`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
	`real_type_id` INT NOT NULL,

	FOREIGN KEY (`real_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE	-- delete key when it's type info is removed
) ENGINE=`INNODB`;
